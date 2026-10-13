using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Provider.Douban
{
    /// <summary>
    /// 豆瓣辅助刮削（电影）：刷新完成后按 IMDb（Wikidata P4529 解析）或标题匹配豆瓣条目，
    /// 仅填充缺失的中文简介/类型/制片地区，评分可选覆盖。带 7 天文件缓存、2 秒频控与风控熔断。
    /// </summary>
    public class DoubanAssist
    {
        private const string BaseUrl = "https://movie.douban.com";
        private const string SuggestUrl = BaseUrl + "/j/subject_suggest";
        private const string SparqlUrl = "https://query.wikidata.org/sparql";
        private const string DoubanProviderId = "Douban";

        private static readonly TimeSpan RequestInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);
        private static readonly TimeSpan BreakerCooldown = TimeSpan.FromHours(6);
        private const int BreakerThreshold = 5;

        private class CacheEntry
        {
            public DateTime Expiry { get; set; }
            public object Value { get; set; }
        }

        public class DoubanSubject
        {
            public string SubjectId { get; set; }
            public double? Rating { get; set; }
            public long? Votes { get; set; }
            public string Summary { get; set; }
            public List<string> Genres { get; set; } = new List<string>();
            public List<string> Countries { get; set; } = new List<string>();
            public List<string> Languages { get; set; } = new List<string>();
        }

        public class DoubanSuggest
        {
            public string id { get; set; }
            public string title { get; set; }
            public string year { get; set; }
            public string type { get; set; }
        }

        private class SparqlResponse
        {
            public SparqlResults results { get; set; }
        }

        private class SparqlResults
        {
            public SparqlBinding[] bindings { get; set; }
        }

        private class SparqlBinding
        {
            public SparqlValue doubanId { get; set; }
        }

        private class SparqlValue
        {
            public string value { get; set; }
        }

        private static readonly Regex RatingRegex =
            new Regex(@"property=""v:average"">\s*([\d.]+)\s*<", RegexOptions.Compiled);
        private static readonly Regex VotesRegex =
            new Regex(@"property=""v:votes"">\s*(\d+)\s*<", RegexOptions.Compiled);
        private static readonly Regex SummaryRegex =
            new Regex(@"property=""v:summary""[^>]*>([\s\S]*?)</span>", RegexOptions.Compiled);
        private static readonly Regex GenreRegex =
            new Regex(@"property=""v:genre"">([^<]+)<", RegexOptions.Compiled);
        private static readonly Regex InfoRegex =
            new Regex(@"id=""info"">([\s\S]*?)</div>", RegexOptions.Compiled);
        private static readonly Regex CountryRegex =
            new Regex(@"制片国家/地区[^<]*</span>\s*([^<\r\n]+)|制片国家/地区:</span?\s*([^<\r\n]+)", RegexOptions.Compiled);
        private static readonly Regex LanguageRegex =
            new Regex(@"语言[^<]*</span>\s*([^<\r\n]+)|语言:</span?\s*([^<\r\n]+)", RegexOptions.Compiled);
        private static readonly Regex StripTagsRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;

        private readonly string _cachePath;
        private readonly object _cacheLock = new object();
        private readonly object _requestLock = new object();
        private readonly ConcurrentDictionary<long, byte> _inFlight = new ConcurrentDictionary<long, byte>();

        private Dictionary<string, CacheEntry> _cache;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private DateTime _breakerUntil = DateTime.MinValue;
        private int _consecutiveBlocked;

        public DoubanAssist(IHttpClient httpClient, IJsonSerializer jsonSerializer)
        {
            _logger = Plugin.Instance.Logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _cachePath = Path.Combine(Plugin.Instance.ApplicationPaths.PluginConfigurationsPath,
                "strmassistant_douban_cache.json");
        }

        /// <summary>
        /// 电影刷新完成后调用：匹配豆瓣条目并填充缺失字段。重复触发、锁定条目与非中文库直接跳过。
        /// </summary>
        public Task ApplyAsync(Movie movie, CancellationToken cancellationToken)
        {
            return ApplyAsync(movie, "movie", cancellationToken);
        }

        /// <summary>
        /// 剧集刷新完成后调用：匹配豆瓣剧集条目并填充缺失字段。
        /// </summary>
        public Task ApplyAsync(Series series, CancellationToken cancellationToken)
        {
            return ApplyAsync(series, "tv", cancellationToken);
        }

        private async Task ApplyAsync(BaseItem item, string suggestType, CancellationToken cancellationToken)
        {
            var options = Plugin.Instance.MetadataEnhanceStore.GetOptions();

            if (!options.EnableDoubanAssist || item == null || item.IsLocked) return;

            var language = item.PreferredMetadataLanguage;
            if (string.IsNullOrEmpty(language) || !language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return;

            if (!_inFlight.TryAdd(item.InternalId, 0)) return;

            try
            {
                var subject = await FindSubjectAsync(item, suggestType, cancellationToken).ConfigureAwait(false);
                if (subject == null) return;

                var changed = false;

                if (!string.Equals(item.GetProviderId(DoubanProviderId), subject.SubjectId, StringComparison.Ordinal))
                {
                    item.ProviderIds[DoubanProviderId] = subject.SubjectId;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(item.Overview) && !string.IsNullOrWhiteSpace(subject.Summary))
                {
                    item.Overview = subject.Summary;
                    changed = true;
                }

                foreach (var genre in subject.Genres)
                {
                    if (!string.IsNullOrWhiteSpace(genre) && !item.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase))
                    {
                        item.AddGenre(genre.Trim());
                        changed = true;
                    }
                }

                if (subject.Countries.Count > 0)
                {
                    var locations = item.ProductionLocations?
                        .Where(l => !string.IsNullOrWhiteSpace(l)).ToList() ?? new List<string>();

                    foreach (var country in subject.Countries)
                    {
                        var trimmed = country.Trim();

                        if (!string.IsNullOrWhiteSpace(trimmed) &&
                            !locations.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                        {
                            locations.Add(trimmed);
                            changed = true;
                        }
                    }

                    item.ProductionLocations = locations.ToArray();
                }

                if (options.DoubanRatingOverride && subject.Rating.HasValue)
                {
                    if (Math.Abs((item.CommunityRating ?? 0) - subject.Rating.Value) > 0.05)
                    {
                        item.CommunityRating = (float)subject.Rating.Value;
                        changed = true;
                    }
                }

                if (changed)
                {
                    item.UpdateToRepository(ItemUpdateType.MetadataEdit);
                    _logger.Info("DoubanAssist - Applied subject {0} to {1}", subject.SubjectId, item.Name);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Warn("DoubanAssist - Failed for {0}: {1}", item.Name, e.Message);
                _logger.Debug(e.StackTrace);
            }
            finally
            {
                _inFlight.TryRemove(item.InternalId, out _);
            }
        }

        private async Task<DoubanSubject> FindSubjectAsync(BaseItem item, string suggestType,
            CancellationToken cancellationToken)
        {
            var cachedId = item.GetProviderId(DoubanProviderId);
            if (!string.IsNullOrEmpty(cachedId))
            {
                return await FetchSubjectAsync(cachedId, cancellationToken).ConfigureAwait(false);
            }

            var imdbId = item.GetProviderId(MetadataProviders.Imdb);
            if (!string.IsNullOrEmpty(imdbId))
            {
                var doubanId = await ResolveDoubanIdByImdbAsync(imdbId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(doubanId))
                {
                    return await FetchSubjectAsync(doubanId, cancellationToken).ConfigureAwait(false);
                }
            }

            var searchTerm = item.OriginalTitle ?? item.Name;
            if (string.IsNullOrWhiteSpace(searchTerm)) return null;

            return await SearchByTitleAsync(searchTerm, item.ProductionYear, suggestType, cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task<DoubanSubject> SearchByTitleAsync(string title, int? year, string suggestType,
            CancellationToken cancellationToken)
        {
            var suggestions = await GetSuggestionsAsync(title, cancellationToken).ConfigureAwait(false);

            var match = suggestions?.FirstOrDefault(s => s.type == suggestType && string.IsNullOrEmpty(s.id) == false);

            if (match == null) return null;

            if (year.HasValue && !string.IsNullOrEmpty(match.year) &&
                int.TryParse(match.year, out var suggestYear) && Math.Abs(suggestYear - year.Value) > 1)
            {
                _logger.Debug("DoubanAssist - Year mismatch for {0}: douban {1} vs item {2}", title, suggestYear, year);
                return null;
            }

            return await FetchSubjectAsync(match.id, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<DoubanSuggest>> GetSuggestionsAsync(string query, CancellationToken cancellationToken)
        {
            var key = "suggest:" + query;

            var cached = GetCache<List<DoubanSuggest>>(key);
            if (cached != null) return cached;

            if (IsBreakerEngaged()) return null;

            var html = await RequestAsync($"{SuggestUrl}?q={Uri.EscapeDataString(query)}", cancellationToken)
                .ConfigureAwait(false);
            if (html == null) return null;

            try
            {
                var result = _jsonSerializer.DeserializeFromString<List<DoubanSuggest>>(html);
                SetCache(key, result);
                return result;
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Suggest parse failed: {0}", e.Message);
                return null;
            }
        }

        /// <summary>
        /// 通过 Wikidata SPARQL（IMDb P345 → 豆瓣 P4529）解析豆瓣条目 ID。
        /// </summary>
        private async Task<string> ResolveDoubanIdByImdbAsync(string imdbId, CancellationToken cancellationToken)
        {
            var key = "wd:" + imdbId;

            var cached = GetCache<string>(key);
            if (cached != null) return cached;

            var query = $"SELECT ?doubanId WHERE {{ ?item wdt:P345 \"{imdbId}\" ; wdt:P4529 ?doubanId. }} LIMIT 1";
            var url = $"{SparqlUrl}?query={Uri.EscapeDataString(query)}&format=json";

            var json = await RequestAsync(url, cancellationToken).ConfigureAwait(false);
            if (json == null) return null;

            try
            {
                var response = _jsonSerializer.DeserializeFromString<SparqlResponse>(json);
                var doubanId = response?.results?.bindings?.FirstOrDefault()?.doubanId?.value;

                SetCache(key, doubanId);

                return doubanId;
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Wikidata parse failed: {0}", e.Message);
                return null;
            }
        }

        private async Task<DoubanSubject> FetchSubjectAsync(string subjectId, CancellationToken cancellationToken)
        {
            var key = "subject:" + subjectId;

            var cached = GetCache<DoubanSubject>(key);
            if (cached != null) return cached;

            if (IsBreakerEngaged()) return null;

            var html = await RequestAsync($"{BaseUrl}/subject/{subjectId}/", cancellationToken).ConfigureAwait(false);
            if (html == null) return null;

            if (html.Contains("sec.douban.com", StringComparison.Ordinal) ||
                (!html.Contains("v:average", StringComparison.Ordinal) &&
                 !html.Contains("id=\"info\"", StringComparison.Ordinal)))
            {
                RegisterBlocked(subjectId);
                return null;
            }

            _consecutiveBlocked = 0;

            var subject = ParseSubject(subjectId, html);
            SetCache(key, subject);

            return subject;
        }

        private static DoubanSubject ParseSubject(string subjectId, string html)
        {
            var subject = new DoubanSubject { SubjectId = subjectId };

            var ratingMatch = RatingRegex.Match(html);
            if (ratingMatch.Success && double.TryParse(ratingMatch.Groups[1].Value, out var rating))
            {
                subject.Rating = rating;
            }

            var votesMatch = VotesRegex.Match(html);
            if (votesMatch.Success && long.TryParse(votesMatch.Groups[1].Value, out var votes))
            {
                subject.Votes = votes;
            }

            var summaryMatch = SummaryRegex.Match(html);
            if (summaryMatch.Success)
            {
                subject.Summary = StripTagsRegex.Replace(summaryMatch.Groups[1].Value, string.Empty)
                    .Replace("&nbsp;", " ").Replace("\u00a0", " ").Trim();
            }

            foreach (Match m in GenreRegex.Matches(html))
            {
                subject.Genres.Add(m.Groups[1].Value.Trim());
            }

            var infoMatch = InfoRegex.Match(html);
            if (infoMatch.Success)
            {
                var info = StripTagsRegex.Replace(infoMatch.Groups[1].Value, string.Empty);

                var countryMatch = CountryRegex.Match(info);
                if (countryMatch.Success)
                {
                    var value = countryMatch.Groups[1].Success ? countryMatch.Groups[1].Value : countryMatch.Groups[2].Value;
                    subject.Countries = value.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
                }

                var languageMatch = LanguageRegex.Match(info);
                if (languageMatch.Success)
                {
                    var value = languageMatch.Groups[1].Success ? languageMatch.Groups[1].Value : languageMatch.Groups[2].Value;
                    subject.Languages = value.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
                }
            }

            return subject;
        }

        private async Task<string> RequestAsync(string url, CancellationToken cancellationToken)
        {
            lock (_requestLock)
            {
                var wait = _lastRequestTime + RequestInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) Thread.Sleep(wait);
                _lastRequestTime = DateTime.UtcNow;
            }

            try
            {
                var requestOptions = new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    AcceptHeader = "text/html,application/json;q=0.9,*/*;q=0.8",
                    BufferContent = true,
                    UserAgent = Plugin.Instance.UserAgent
                };

                var cookie = Plugin.Instance.MetadataEnhanceStore.GetOptions().DoubanCookie;
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    requestOptions.RequestHeaders["Cookie"] = cookie.Trim();
                }

                using var response = await _httpClient.SendAsync(requestOptions, "GET").ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Debug("DoubanAssist - {0} returned {1}", url, response.StatusCode);
                    return null;
                }

                await using var contentStream = response.Content;
                using var reader = new StreamReader(contentStream);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Request failed for {0}: {1}", url, e.Message);
                return null;
            }
        }

        private bool IsBreakerEngaged()
        {
            if (_breakerUntil > DateTime.UtcNow)
            {
                _logger.Warn("DoubanAssist - Circuit breaker engaged until {0:HH:mm}, requests paused", _breakerUntil);
                return true;
            }

            return false;
        }

        private void RegisterBlocked(string subjectId)
        {
            _consecutiveBlocked++;

            _logger.Warn("DoubanAssist - Blocked by anti-crawler for subject {0} ({1}/{2})",
                subjectId, _consecutiveBlocked, BreakerThreshold);

            if (_consecutiveBlocked >= BreakerThreshold)
            {
                _breakerUntil = DateTime.UtcNow + BreakerCooldown;
                _consecutiveBlocked = 0;
                _logger.Warn("DoubanAssist - Circuit breaker engaged for {0} hours, configure Cookie or pause scraping",
                    BreakerCooldown.TotalHours);
            }
        }

        private T GetCache<T>(string key) where T : class
        {
            lock (_cacheLock)
            {
                if (_cache == null) LoadCache();

                if (_cache.TryGetValue(key, out var entry))
                {
                    if (entry.Expiry > DateTime.UtcNow) return entry.Value as T;
                    _cache.Remove(key);
                }
            }

            return null;
        }

        private void SetCache<T>(string key, T value) where T : class
        {
            lock (_cacheLock)
            {
                if (_cache == null) LoadCache();

                _cache[key] = new CacheEntry { Expiry = DateTime.UtcNow + CacheDuration, Value = value };
                SaveCache();
            }
        }

        private void LoadCache()
        {
            _cache = new Dictionary<string, CacheEntry>();

            try
            {
                if (File.Exists(_cachePath))
                {
                    var persisted = _jsonSerializer.DeserializeFromFile<Dictionary<string, CacheEntry>>(_cachePath);
                    if (persisted != null) _cache = persisted;
                }
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Cache load failed: {0}", e.Message);
            }
        }

        private void SaveCache()
        {
            try
            {
                var cutoff = DateTime.UtcNow;

                foreach (var expired in _cache.Where(kv => kv.Value.Expiry <= cutoff)
                             .Select(kv => kv.Key).ToList())
                {
                    _cache.Remove(expired);
                }

                _jsonSerializer.SerializeToFile(_cache, _cachePath);
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Cache save failed: {0}", e.Message);
            }
        }
    }
}
