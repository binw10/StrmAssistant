using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Common
{
    /// <summary>
    /// TheIntroDB v3 API（https://theintrodb.org）片头片尾数据客户端与刮削编排。
    /// 数据写入为带 #DB 后缀的章节标记，仅填充缺失标记或更新自身 #DB 标记。
    /// </summary>
    public class IntroDbApi
    {
        private const string BaseUrl = "https://api.theintrodb.org/v3";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
        private static readonly TimeSpan RequestInterval = TimeSpan.FromMilliseconds(250);

        public class IntroDbRateLimitException : Exception
        {
            public IntroDbRateLimitException() : base("IntroDb daily rate limit reached (429)")
            {
            }
        }

        public class IntroDbSegment
        {
            public long? start_ms { get; set; }
            public long? end_ms { get; set; }
        }

        public class IntroDbMedia
        {
            public List<IntroDbSegment> intro { get; set; }
            public List<IntroDbSegment> credits { get; set; }
        }

        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;

        private readonly ConcurrentDictionary<string, (DateTime Expiry, IntroDbMedia Data)> _cache =
            new ConcurrentDictionary<string, (DateTime, IntroDbMedia)>();

        private readonly ConcurrentDictionary<long, byte> _scrapingItems =
            new ConcurrentDictionary<long, byte>();

        private DateTime _lastRequestTime = DateTime.MinValue;

        public IntroDbApi(IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _logger = Plugin.Instance.Logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// 查询单个媒体（剧集传 season/episode，电影传 type=movie）的片头片尾数据，带 6 小时缓存。
        /// 无数据返回 null；触发限额抛 IntroDbRateLimitException。
        /// </summary>
        public async Task<IntroDbMedia> GetMediaInfoAsync(string tmdbId, int? season, int? episode,
            CancellationToken cancellationToken)
        {
            var url = season.HasValue && episode.HasValue
                ? $"{BaseUrl}/media?tmdb_id={tmdbId}&season={season.Value}&episode={episode.Value}"
                : $"{BaseUrl}/media?tmdb_id={tmdbId}&type=movie";

            if (_cache.TryGetValue(url, out var cached) && cached.Expiry > DateTime.UtcNow)
            {
                return cached.Data;
            }

            await ThrottleAsync().ConfigureAwait(false);

            var requestOptions = new HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                AcceptHeader = "application/json",
                BufferContent = true,
                UserAgent = Plugin.Instance.UserAgent
            };

            var apiKey = Plugin.Instance.IntroSkipStore.GetOptions().IntroDbApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                requestOptions.RequestHeaders["Authorization"] = "Bearer " + apiKey.Trim();
            }

            try
            {
                using var response = await _httpClient.SendAsync(requestOptions, "GET").ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _cache[url] = (DateTime.UtcNow + CacheDuration, null);
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new IntroDbRateLimitException();
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Warn("IntroDbScrape - Unexpected status {0} for {1}", response.StatusCode, url);
                    return null;
                }

                await using var contentStream = response.Content;
                var data = _jsonSerializer.DeserializeFromStream<IntroDbMedia>(contentStream);
                _cache[url] = (DateTime.UtcNow + CacheDuration, data);

                return data;
            }
            catch (IntroDbRateLimitException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Warn("IntroDbScrape - Request failed for {0}: {1}", url, e.Message);
                return null;
            }
        }

        /// <summary>
        /// 刮削一部剧集的全部缺失片头片尾标记。返回写入条数。
        /// </summary>
        public async Task<int> ScrapeSeriesAsync(Series series, CancellationToken cancellationToken)
        {
            if (!_scrapingItems.TryAdd(series.InternalId, 0)) return 0;

            try
            {
                var tmdbId = series.GetProviderId(MetadataProviders.Tmdb);
                if (string.IsNullOrEmpty(tmdbId)) return 0;

                var episodes = series
                    .GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        Recursive = true,
                        IncludeItemTypes = new[] { nameof(Episode) }
                    })
                    .OfType<Episode>()
                    .Where(e => e.IndexNumber.HasValue)
                    .OrderBy(e => e.ParentIndexNumber ?? 0)
                    .ThenBy(e => e.IndexNumber ?? 0)
                    .ToList();

                var marked = 0;
                var consecutiveNotFound = 0;

                foreach (var episode in episodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (Plugin.ChapterApi.HasIntro(episode) && Plugin.ChapterApi.HasCredits(episode)) continue;

                    if (consecutiveNotFound >= 50)
                    {
                        _logger.Warn("IntroDbScrape - Aborted after 50 consecutive items without data for {0}",
                            series.Name);
                        break;
                    }

                    IntroDbMedia data;

                    try
                    {
                        data = await GetMediaInfoAsync(tmdbId, episode.ParentIndexNumber, episode.IndexNumber,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (IntroDbRateLimitException)
                    {
                        _logger.Warn("IntroDbScrape - Rate limit reached, stop scraping {0} this round", series.Name);
                        break;
                    }

                    if (data == null)
                    {
                        consecutiveNotFound++;
                        continue;
                    }

                    consecutiveNotFound = 0;

                    if (Plugin.ChapterApi.UpdateIntroDbMarkers(episode, data.intro, data.credits))
                    {
                        marked++;
                    }
                }

                if (marked > 0)
                {
                    _logger.Info("IntroDbScrape - Marked {0} episode(s) for {1}", marked, series.Name);
                }

                return marked;
            }
            finally
            {
                _scrapingItems.TryRemove(series.InternalId, out _);
            }
        }

        /// <summary>
        /// 刮削一部电影的缺失片头片尾标记。返回是否写入。
        /// </summary>
        public async Task<bool> ScrapeMovieAsync(Movie movie, CancellationToken cancellationToken)
        {
            if (!_scrapingItems.TryAdd(movie.InternalId, 0)) return false;

            try
            {
                var tmdbId = movie.GetProviderId(MetadataProviders.Tmdb);
                if (string.IsNullOrEmpty(tmdbId)) return false;

                IntroDbMedia data;

                try
                {
                    data = await GetMediaInfoAsync(tmdbId, null, null, cancellationToken).ConfigureAwait(false);
                }
                catch (IntroDbRateLimitException)
                {
                    _logger.Warn("IntroDbScrape - Rate limit reached, skip movie {0}", movie.Name);
                    return false;
                }

                if (data == null) return false;

                return Plugin.ChapterApi.UpdateIntroDbMarkers(movie, data.intro, data.credits);
            }
            finally
            {
                _scrapingItems.TryRemove(movie.InternalId, out _);
            }
        }

        private async Task ThrottleAsync()
        {
            var wait = _lastRequestTime + RequestInterval - DateTime.UtcNow;

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait).ConfigureAwait(false);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
    }
}
