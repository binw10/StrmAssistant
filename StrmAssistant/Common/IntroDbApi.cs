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
    /// Unified IntroDb API：IntroDb（api.introdb.app，IMDb 定位）与 Tidb（api.theintrodb.org，TMDB 定位）
    /// 双源按序尝试。数据写入为带 #DB 后缀的章节标记，仅填充缺失标记或更新自身 #DB 标记。
    /// </summary>
    public class IntroDbApi
    {
        private const string IntroDbAppBaseUrl = "https://api.introdb.app";
        private const string TheIntroDbBaseUrl = "https://api.theintrodb.org/v3";
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
        /// 查询单个媒体的片头片尾数据，按 UnifiedIntroDbProviders 顺序尝试双源，带 6 小时缓存。
        /// IntroDb（introdb.app）按 IMDb ID 定位；Tidb（theintrodb.org）按 TMDB ID 定位。
        /// 无数据返回 null；触发限额抛 IntroDbRateLimitException。
        /// </summary>
        public async Task<IntroDbMedia> GetMediaInfoAsync(string tmdbId, int? season, int? episode,
            CancellationToken cancellationToken, string imdbId = null, long? durationMs = null)
        {
            var providers = ParseUnifiedProviders(
                Plugin.Instance.IntroSkipStore.GetOptions().UnifiedIntroDbProviders);

            foreach (var provider in providers)
            {
                var data = await QueryProviderAsync(provider, tmdbId, imdbId, durationMs, season, episode,
                    cancellationToken).ConfigureAwait(false);

                if (data != null &&
                    ((data.intro?.Count ?? 0) > 0 || (data.credits?.Count ?? 0) > 0))
                {
                    return data;
                }
            }

            return null;
        }

        private static List<string> ParseUnifiedProviders(string raw)
        {
            var list = (raw ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Equals("IntroDb", StringComparison.OrdinalIgnoreCase) ||
                            p.Equals("Tidb", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Equals("Tidb", StringComparison.OrdinalIgnoreCase) ? "Tidb" : "IntroDb")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0) list.Add("IntroDb");
            if (!list.Contains("Tidb")) list.Add("Tidb");

            return list;
        }

        private async Task<IntroDbMedia> QueryProviderAsync(string provider, string tmdbId, string imdbId,
            long? durationMs, int? season, int? episode, CancellationToken cancellationToken)
        {
            string url;
            string apiKey;

            if (provider == "IntroDb")
            {
                if (string.IsNullOrWhiteSpace(imdbId))
                {
                    _logger.Debug("IntroDbScrape - IntroDb source skipped (no IMDb id)");
                    return null;
                }

                apiKey = Plugin.Instance.IntroSkipStore.GetOptions().IntroDbApiKey;
                url = season.HasValue && episode.HasValue
                    ? $"{IntroDbAppBaseUrl}/segments?imdb_id={Uri.EscapeDataString(imdbId)}&season={season.Value}&episode={episode.Value}"
                    : $"{IntroDbAppBaseUrl}/segments?imdb_id={Uri.EscapeDataString(imdbId)}";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(tmdbId)) return null;

                apiKey = Plugin.Instance.IntroSkipStore.GetOptions().TidbApiKey;
                url = season.HasValue && episode.HasValue
                    ? $"{TheIntroDbBaseUrl}/media?tmdb_id={tmdbId}&season={season.Value}&episode={episode.Value}"
                    : $"{TheIntroDbBaseUrl}/media?tmdb_id={tmdbId}&type=movie";

                if (durationMs.HasValue && durationMs.Value > 0)
                {
                    url += $"&duration_ms={durationMs.Value}";
                }
            }

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
                    _logger.Warn("IntroDbScrape - {0} rate limit reached", provider);
                    return null;
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Warn("IntroDbScrape - {0} request failed for {1}: {2}", provider, url, e.Message);
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

                    // 仅有 IntroDb 自身 #DB 标记的集允许重新刮削刷新；探测等外部来源不覆盖
                    if (Plugin.ChapterApi.HasNonDbIntro(episode) && Plugin.ChapterApi.HasCredits(episode)) continue;

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
                            cancellationToken,
                            imdbId: episode.Series?.GetProviderId(MetadataProviders.Imdb),
                            durationMs: episode.RunTimeTicks > 0 ? episode.RunTimeTicks / 10000 : null)
                            .ConfigureAwait(false);
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
                    data = await GetMediaInfoAsync(tmdbId, null, null, cancellationToken,
                        imdbId: movie.GetProviderId(MetadataProviders.Imdb),
                        durationMs: movie.RunTimeTicks > 0 ? movie.RunTimeTicks / 10000 : null)
                        .ConfigureAwait(false);
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
