using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
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
using DoubanScrapeOption = StrmAssistant.Options.DoubanAssistScrapeOption;

namespace StrmAssistant.Provider.Douban
{
    /// <summary>
    /// 豆瓣辅助刮削（电影）：刷新完成后按 IMDb（Wikidata P4529 解析）或标题匹配豆瓣条目，
    /// 仅填充缺失的中文简介/类型/制片地区，评分可选覆盖。带 7 天文件缓存、2 秒频控与风控熔断。
    /// </summary>
    public class DoubanAssist
    {
        private const string BaseUrl = "https://movie.douban.com";

        private const string MdbListBaseUrl = "https://api.mdblist.com";

        /// <summary>
        /// MdbList 评分源：按 IMDb ID 拉取烂番茄/Metacritic 等评分，
        /// 写入 CriticRating（烂番茄 %）与 CriticRatingSummary（评分汇总）。
        /// </summary>
        public async Task<bool> UpdateMdbListRatingsAsync(BaseItem item, string apiKey,
            CancellationToken cancellationToken)
        {
            var imdbId = item.GetProviderId(MetadataProviders.Imdb);
            if (string.IsNullOrWhiteSpace(imdbId)) return false;

            try
            {
                var url = $"{MdbListBaseUrl}/imdb/{imdbId.Trim()}?apikey={Uri.EscapeDataString(apiKey.Trim())}&format=json";

                var requestOptions = new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    AcceptHeader = "application/json",
                    BufferContent = true,
                    UserAgent = Plugin.Instance.UserAgent
                };

                using var response = await _httpClient.SendAsync(requestOptions, "GET").ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Debug("MdbList - Unexpected status {0} for {1}", response.StatusCode, imdbId);
                    return false;
                }

                await using var contentStream = response.Content;
                var payload = _jsonSerializer.DeserializeFromStream<MdbListResponse>(contentStream);
                var ratings = payload?.ratings;
                if (ratings == null || ratings.Count == 0) return false;

                double? tomatoes = null;
                double? imdb = null;
                double? metacritic = null;

                foreach (var rating in ratings)
                {
                    if (rating.value == null) continue;

                    var source = rating.source ?? string.Empty;
                    if (source.IndexOf("tomato", StringComparison.OrdinalIgnoreCase) >= 0)
                        tomatoes = rating.value;
                    else if (source.IndexOf("imdb", StringComparison.OrdinalIgnoreCase) >= 0)
                        imdb = rating.value;
                    else if (source.IndexOf("metacritic", StringComparison.OrdinalIgnoreCase) >= 0)
                        metacritic = rating.value;
                }

                if (!tomatoes.HasValue) return false;

                var changed = false;

                var newCriticRating = (float)Math.Round(tomatoes.Value, 0);
                if (Math.Abs((item.CriticRating ?? 0) - newCriticRating) > 0.01)
                {
                    item.CriticRating = newCriticRating;
                    changed = true;
                }

                // 4.9 的 BaseItem 无 CriticRatingSummary，汇总仅记日志
                var summaryParts = new List<string>();
                if (imdb.HasValue) summaryParts.Add($"IMDb: {imdb.Value:0.0}");
                if (metacritic.HasValue) summaryParts.Add($"Metacritic: {metacritic.Value:0}");
                summaryParts.Add($"Rotten Tomatoes: {tomatoes.Value:0}%");

                if (changed)
                {
                    _logger.Info("MdbList - Ratings updated for {0}: {1}", item.Name,
                        string.Join(" · ", summaryParts));
                }

                return changed;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Debug("MdbList - Request failed for {0}: {1}", imdbId, ex.Message);
                return false;
            }
        }

        private class MdbListResponse
        {
            public List<MdbListRating> ratings { get; set; }
        }

        private class MdbListRating
        {
            public string source { get; set; }
            public double? value { get; set; }
        }
        private const string SuggestUrl = BaseUrl + "/j/subject_suggest";
        private const string SparqlUrl = "https://query.wikidata.org/sparql";
        private const string DoubanProviderId = "Douban";

        // 豆瓣开放平台 V2 API（PRO 同构）：配置 DoubanAccessToken 后优先走官方接口，
        // 可绕过网页端风控，且返回结构化 JSON。未配置 Token 时回退到 movie.douban.com 网页刮削。
        private const string DoubanV2BaseUrl = "https://api.douban.com/v2";
        private const string DoubanV2MoviePath = "/movie/{0}";
        private const string DoubanV2TvPath = "/tv/{0}";

        private static readonly TimeSpan RequestInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);
        private static readonly TimeSpan BreakerCooldown = TimeSpan.FromHours(6);
        private const int BreakerThreshold = 5;

        private class CacheEntry
        {
            public DateTime Expiry { get; set; }
            public object Value { get; set; }
        }

        /// <summary>
        /// 评分快照（无过期）：豆瓣评分徽章数据源，条目通过 ProviderIds["Douban"] 关联。
        /// </summary>
        public class DoubanRatingSnapshot
        {
            public double Rating { get; set; }
            public long? Votes { get; set; }
            public DateTime Updated { get; set; }
        }

        public class DoubanRatingInfo
        {
            public string ItemId { get; set; }
            public string SubjectId { get; set; }
            public double? Rating { get; set; }
            public long? Votes { get; set; }
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
            public string img { get; set; }
        }

        public class DoubanCelebrity
        {
            public string ChineseName { get; set; }
            public string ForeignName { get; set; }
            public string Role { get; set; }
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
        private static readonly Regex CelebrityNameRegex =
            new Regex(@"class=""name""[^>]*>\s*<a href=""[^""]*celebrity/\d+/?""[^>]*>([^<]+)</a>\s*(?:/\s*([^<]*?))?\s*</span>",
                RegexOptions.Compiled);
        private static readonly Regex CelebrityRoleRegex = new Regex(@"饰[:：]?\s*([^<]+)", RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger _logger;

        private readonly string _cachePath;
        private readonly string _ratingsPath;
        private readonly object _cacheLock = new object();
        private readonly object _ratingsLock = new object();
        private readonly object _requestLock = new object();
        private readonly ConcurrentDictionary<long, byte> _inFlight = new ConcurrentDictionary<long, byte>();

        private Dictionary<string, CacheEntry> _cache;
        private Dictionary<string, DoubanRatingSnapshot> _ratings;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private DateTime _breakerUntil = DateTime.MinValue;
        private int _consecutiveBlocked;

        public DoubanAssist(IHttpClient httpClient, IJsonSerializer jsonSerializer,
            IProviderManager providerManager, IFileSystem fileSystem)
        {
            _logger = Plugin.Instance.Logger;
            _httpClient = httpClient;
            _jsonSerializer = jsonSerializer;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _cachePath = Path.Combine(Plugin.Instance.ApplicationPaths.PluginConfigurationsPath,
                "strmassistant_douban_cache.json");
            _ratingsPath = Path.Combine(Plugin.Instance.ApplicationPaths.PluginConfigurationsPath,
                "strmassistant_douban_ratings.json");
        }

        /// <summary>
        /// 按条目 ID 列表解析豆瓣评分快照（经条目上的 Douban ProviderId 关联），供徽章端点使用。
        /// </summary>
        public List<DoubanRatingInfo> GetRatings(IEnumerable<string> itemIds)
        {
            var result = new List<DoubanRatingInfo>();

            lock (_ratingsLock)
            {
                if (_ratings == null) LoadRatings();

                if (_ratings.Count == 0) return result;

                foreach (var itemId in itemIds)
                {
                    try
                    {
                        var item = Plugin.LibraryManager.GetItemById(long.Parse(itemId));
                        var subjectId = item?.GetProviderId(DoubanProviderId);

                        if (string.IsNullOrEmpty(subjectId)) continue;

                        if (_ratings.TryGetValue(subjectId, out var snapshot))
                        {
                            result.Add(new DoubanRatingInfo
                            {
                                ItemId = itemId,
                                SubjectId = subjectId,
                                Rating = snapshot.Rating,
                                Votes = snapshot.Votes
                            });
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return result;
        }

        private void RecordRatingSnapshot(string subjectId, double rating, long? votes)
        {
            lock (_ratingsLock)
            {
                if (_ratings == null) LoadRatings();

                if (_ratings.TryGetValue(subjectId, out var existing) &&
                    Math.Abs(existing.Rating - rating) < 0.001 &&
                    existing.Votes == votes)
                {
                    return;
                }

                _ratings[subjectId] = new DoubanRatingSnapshot
                {
                    Rating = rating,
                    Votes = votes,
                    Updated = DateTime.UtcNow
                };

                SaveRatings();
            }
        }

        private void LoadRatings()
        {
            _ratings = new Dictionary<string, DoubanRatingSnapshot>();

            try
            {
                if (File.Exists(_ratingsPath))
                {
                    var persisted = _jsonSerializer.DeserializeFromFile<Dictionary<string, DoubanRatingSnapshot>>(_ratingsPath);
                    if (persisted != null) _ratings = persisted;
                }
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Ratings load failed: {0}", e.Message);
            }
        }

        private void SaveRatings()
        {
            try
            {
                _jsonSerializer.SerializeToFile(_ratings, _ratingsPath);
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Ratings save failed: {0}", e.Message);
            }
        }

        /// <summary>
        /// 电影刷新完成后调用：匹配豆瓣条目并填充缺失字段。重复触发、锁定条目与非中文库直接跳过。
        /// </summary>
        public Task ApplyAsync(Movie movie, CancellationToken cancellationToken)
        {
            return ApplyAsync(movie, "movie", null, cancellationToken);
        }

        /// <summary>
        /// 剧集刷新完成后调用：匹配豆瓣剧集条目并填充缺失字段。
        /// </summary>
        public Task ApplyAsync(Series series, CancellationToken cancellationToken)
        {
            return ApplyAsync(series, "tv", null, cancellationToken);
        }

        /// <summary>
        /// 季刷新完成后调用：按「剧名 第N季」匹配豆瓣季条目（阿拉伯/中文数字各试一次）。
        /// </summary>
        /// <summary>
        /// 「豆瓣辅助生效范围」= 允许豆瓣改写的字段（与 PRO 的 DoubanAssistScrapeOption 一致）。
        /// 空值或旧版库 ID 值（不含任何已知字段名）一律视为「全部」。
        /// </summary>
        private static bool IsScrapeFieldEnabled(StrmAssistant.Options.MetadataEnhanceOptions options,
            DoubanScrapeOption option)
        {
            var scope = options.DoubanAssist.DoubanAssistScrapeScope;
            if (string.IsNullOrWhiteSpace(scope)) return true;

            var tokens = scope.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

            if (tokens.Length == 0) return true;

            var known = Enum.GetNames(typeof(DoubanScrapeOption));
            var hasKnown = tokens.Any(t => known.Any(k => string.Equals(k, t, StringComparison.OrdinalIgnoreCase)));

            // 兼容旧值：以前该字段存的是媒体库 ID 列表，不认识任何字段名 → 视为全部
            if (!hasKnown) return true;

            return tokens.Any(t => string.Equals(t, option.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        public Task ApplyAsync(Season season, CancellationToken cancellationToken)
        {
            return ApplyAsync(season, "tv", season?.IndexNumber, cancellationToken);
        }

        /// <summary>
        /// 构建缓存：仅预取豆瓣数据落盘（subject + 评分快照），不写回条目。
        /// 已有豆瓣 ID 且缓存未过期的条目直接跳过，不产生网络请求。
        /// </summary>
        public async Task<bool> BuildCacheAsync(BaseItem item, CancellationToken cancellationToken)
        {
            var options = Plugin.Instance.MetadataEnhanceStore.GetOptions();
            if (!options.DoubanAssist.EnableDoubanAssist || item == null || item.IsLocked) return false;

            var language = ResolveMetadataLanguage(item);
            if (string.IsNullOrEmpty(language) || !language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return false;

            var suggestType = item is Movie ? "movie" : "tv";
            var cachedId = item.GetProviderId(DoubanProviderId);

            lock (_cacheLock)
            {
                if (_cache == null) LoadCache();
                if (!string.IsNullOrEmpty(cachedId) &&
                    _cache.TryGetValue("subject:" + cachedId, out var entry) &&
                    entry.Expiry > DateTime.UtcNow)
                {
                    return false;
                }
            }

            try
            {
                var subject = await FindSubjectAsync(item, suggestType, null, cancellationToken).ConfigureAwait(false);
                return subject != null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warn("DoubanCacheBuild - Failed for {0}: {1}", item.Name, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 解析条目生效的元数据语言：条目级为空时回退到所属媒体库的 LibraryOptions。
        /// （Emby 4.10 的 item.PreferredMetadataLanguage 不再继承库设置，实测返回空。）
        /// </summary>
        private static string ResolveMetadataLanguage(BaseItem item)
        {
            if (!string.IsNullOrEmpty(item.PreferredMetadataLanguage)) return item.PreferredMetadataLanguage;

            try
            {
                return BaseItem.LibraryManager.GetLibraryOptions(item)?.PreferredMetadataLanguage;
            }
            catch
            {
                return null;
            }
        }

        private async Task ApplyAsync(BaseItem item, string suggestType, int? seasonNumber,
            CancellationToken cancellationToken)
        {
            var options = Plugin.Instance.MetadataEnhanceStore.GetOptions();

            if (!options.DoubanAssist.EnableDoubanAssist || item == null || item.IsLocked) return;

            var language = ResolveMetadataLanguage(item);
            if (string.IsNullOrEmpty(language) || !language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("DoubanAssist - Skipped (language={0}): {1}", language ?? "(empty)", item.Name);
                return;
            }

            if (!_inFlight.TryAdd(item.InternalId, 0)) return;

            try
            {
                var subject = await FindSubjectAsync(item, suggestType, seasonNumber, cancellationToken).ConfigureAwait(false);
                if (subject == null)
                {
                    _logger.Info("DoubanAssist - No douban match for: {0}", item.Name);
                    return;
                }

                _logger.Info("DoubanAssist - Matched {0} -> subject {1}", item.Name, subject.SubjectId);

                var changed = false;

                // 「豆瓣辅助生效范围」字段门控（空值/旧值 = 全部）
                var allowNameOverview = IsScrapeFieldEnabled(options, DoubanScrapeOption.NameOverview);
                var allowRating = IsScrapeFieldEnabled(options, DoubanScrapeOption.Rating);
                var allowCast = IsScrapeFieldEnabled(options, DoubanScrapeOption.CastCharacter);
                var allowPoster = IsScrapeFieldEnabled(options, DoubanScrapeOption.Poster);

                if (!string.Equals(item.GetProviderId(DoubanProviderId), subject.SubjectId, StringComparison.Ordinal))
                {
                    item.ProviderIds[DoubanProviderId] = subject.SubjectId;
                    changed = true;
                }

                if (allowNameOverview)
                {
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
                }

                if (options.DoubanAssist.DoubanRatingOverride && allowRating && subject.Rating.HasValue)
                {
                    // RatingUpdateLookbackDays：评分更新只作用于首播日期在回看期内的条目
                    var lookbackDays = options.DoubanAssist.RatingUpdateLookbackDays;
                    var ratingAllowed = lookbackDays <= 0 || !item.PremiereDate.HasValue ||
                                        item.PremiereDate.Value >= DateTime.UtcNow.AddDays(-lookbackDays);

                    if (ratingAllowed && Math.Abs((item.CommunityRating ?? 0) - subject.Rating.Value) > 0.05)
                    {
                        item.CommunityRating = (float)subject.Rating.Value;
                        changed = true;
                        _logger.Info("DoubanAssist - Rating updated for {0}: {1} (douban {2}, votes {3})",
                            item.Name, subject.Rating.Value, subject.Rating.Value, subject.Votes);
                    }
                    else if (!ratingAllowed)
                    {
                        _logger.Info("DoubanAssist - Rating skipped (outside lookback {0}d): {1}", lookbackDays, item.Name);
                    }
                }

                var ratingInLookback = options.DoubanAssist.RatingUpdateLookbackDays <= 0 || !item.PremiereDate.HasValue ||
                                       item.PremiereDate.Value >= DateTime.UtcNow.AddDays(-options.DoubanAssist.RatingUpdateLookbackDays);

                if (ratingInLookback && allowRating && !string.IsNullOrWhiteSpace(options.DoubanAssist.MdbListApiKey))
                {
                    changed = await UpdateMdbListRatingsAsync(item, options.DoubanAssist.MdbListApiKey, cancellationToken)
                        .ConfigureAwait(false) || changed;
                }

                if (options.DoubanAssist.DoubanCastChinese && allowCast)
                {
                    changed = await ApplyCastChineseNamesAsync(item, subject.SubjectId, cancellationToken)
                        .ConfigureAwait(false) || changed;
                }

                if (options.DoubanAssist.DoubanPosterFallback && allowPoster && !item.HasImage(ImageType.Primary) && suggestType == "movie")
                {
                    changed = await TrySetDoubanPosterAsync(item, subject.SubjectId, cancellationToken)
                        .ConfigureAwait(false) || changed;
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

        /// <summary>
        /// 豆瓣演员中文名：仅替换当前为纯英文名的演员（匹配豆瓣外文名），已中文名的演员不动。
        /// </summary>
        private async Task<bool> ApplyCastChineseNamesAsync(BaseItem item, string subjectId,
            CancellationToken cancellationToken)
        {
            var people = Plugin.LibraryManager.GetItemPeople(item);

            if (people == null || people.Count == 0) return false;

            var celebrities = await FetchCelebritiesAsync(subjectId, cancellationToken).ConfigureAwait(false);

            if (celebrities == null || celebrities.Count == 0) return false;

            var changed = false;

            foreach (var person in people.Where(p =>
                         p.Type == PersonType.Actor && !string.IsNullOrWhiteSpace(p.Name)))
            {
                if (person.Name.Any(c => c > 0x7F)) continue;

                var entry = celebrities.FirstOrDefault(c =>
                    !string.IsNullOrEmpty(c.ForeignName) &&
                    string.Equals(c.ForeignName.Trim(), person.Name.Trim(), StringComparison.OrdinalIgnoreCase));

                if (entry == null || string.IsNullOrEmpty(entry.ChineseName) ||
                    !entry.ChineseName.Any(c => c > 0x7F)) continue;

                _logger.Info("DoubanAssist - Cast {0} -> {1} for {2}", person.Name, entry.ChineseName, item.Name);
                person.Name = entry.ChineseName;
                changed = true;
            }

            if (changed)
            {
                Plugin.LibraryManager.UpdatePeople(item, people);
            }

            return changed;
        }

        private async Task<List<DoubanCelebrity>> FetchCelebritiesAsync(string subjectId,
            CancellationToken cancellationToken)
        {
            var key = "celeb:" + subjectId;

            var cached = GetCache<List<DoubanCelebrity>>(key);
            if (cached != null) return cached;

            var html = await RequestAsync($"{BaseUrl}/subject/{subjectId}/celebrities", cancellationToken)
                .ConfigureAwait(false);
            if (html == null) return null;

            var result = new List<DoubanCelebrity>();
            var matches = CelebrityNameRegex.Matches(html).Cast<Match>().ToList();

            for (var i = 0; i < matches.Count; i++)
            {
                var celebrity = new DoubanCelebrity { ChineseName = matches[i].Groups[1].Value.Trim() };

                var foreign = matches[i].Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(foreign))
                {
                    celebrity.ForeignName = foreign.Trim();
                }

                var blockEnd = i + 1 < matches.Count ? matches[i + 1].Index : Math.Min(html.Length, matches[i].Index + 800);
                var roleMatch = CelebrityRoleRegex.Match(html, matches[i].Index, blockEnd - matches[i].Index);
                if (roleMatch.Success)
                {
                    celebrity.Role = roleMatch.Groups[1].Value.Trim();
                }

                result.Add(celebrity);
            }

            SetCache(key, result);

            return result;
        }

        /// <summary>
        /// 豆瓣海报兜底：条目无主图时，从 suggest 结果取海报（优先大图）写入 Primary。
        /// </summary>
        private async Task<bool> TrySetDoubanPosterAsync(BaseItem item, string subjectId,
            CancellationToken cancellationToken)
        {
            try
            {
                var searchTerm = item.OriginalTitle ?? item.Name;

                if (string.IsNullOrWhiteSpace(searchTerm)) return false;

                var suggestions = await GetSuggestionsAsync(searchTerm, cancellationToken).ConfigureAwait(false);
                var posterUrl = suggestions?
                    .Where(s => string.Equals(s.id, subjectId, StringComparison.Ordinal) && !string.IsNullOrEmpty(s.img))
                    .Select(s => s.img)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(posterUrl)) return false;

                var largeUrl = posterUrl.Replace("/s_ratio_poster/", "/l_ratio_poster/", StringComparison.Ordinal);

                var tempPath = await DownloadToTempAsync(largeUrl, cancellationToken).ConfigureAwait(false)
                               ?? await DownloadToTempAsync(posterUrl, cancellationToken).ConfigureAwait(false);

                if (tempPath == null) return false;

                var libraryOptions = Plugin.LibraryManager.GetLibraryOptions(item);
                var directoryService = new DirectoryService(_logger, _fileSystem);

                await _providerManager.SaveImage(item, libraryOptions, tempPath, ImageType.Primary, null,
                        Array.Empty<long>(), directoryService, false, cancellationToken)
                    .ConfigureAwait(false);

                _logger.Info("DoubanAssist - Poster set from subject {0} for {1}", subjectId, item.Name);

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Warn("DoubanAssist - Poster fallback failed for {0}: {1}", item.Name, e.Message);
                return false;
            }
        }

        private async Task<string> DownloadToTempAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var requestOptions = new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken,
                    BufferContent = true,
                    UserAgent = Plugin.Instance.UserAgent
                };

                var cookie = Plugin.Instance.MetadataEnhanceStore.GetOptions().DoubanAssist.DoubanCookie;
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    requestOptions.RequestHeaders["Cookie"] = cookie.Trim();
                }

                using var response = await _httpClient.SendAsync(requestOptions, "GET").ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK) return null;

                var tempPath = Path.Combine(Plugin.Instance.ApplicationPaths.TempDirectory,
                    "douban_" + Guid.NewGuid().ToString("N") + ".jpg");
                Directory.CreateDirectory(Plugin.Instance.ApplicationPaths.TempDirectory);

                await using var contentStream = response.Content;
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                return tempPath;
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - Image download failed for {0}: {1}", url, e.Message);
                return null;
            }
        }

        private async Task<DoubanSubject> FindSubjectAsync(BaseItem item, string suggestType, int? seasonNumber,
            CancellationToken cancellationToken)
        {
            var cachedId = item.GetProviderId(DoubanProviderId);
            if (!string.IsNullOrEmpty(cachedId))
            {
                return await FetchSubjectAsync(cachedId, cancellationToken).ConfigureAwait(false);
            }

            if (seasonNumber.HasValue)
            {
                var season = item as Season;
                var seriesTitle = season?.Series != null
                    ? season.Series.OriginalTitle ?? season.Series.Name
                    : null;

                if (string.IsNullOrEmpty(seriesTitle)) return null;

                var candidates = new List<string> { $"{seriesTitle} 第{seasonNumber.Value}季" };
                var chinese = ToChineseSeasonNumber(seasonNumber.Value);
                if (!chinese.Contains(seasonNumber.Value.ToString())) candidates.Add($"{seriesTitle} {chinese}");

                foreach (var query in candidates)
                {
                    var subject = await SearchByTitleAsync(query, null, suggestType, seasonNumber, cancellationToken)
                        .ConfigureAwait(false);
                    if (subject != null) return subject;
                }

                // 第三策略：按剧名搜索，从全部 tv 建议里找标题带季标记的条目
                var suggestions = await GetSuggestionsAsync(seriesTitle, cancellationToken).ConfigureAwait(false);

                var seasonMatch = suggestions?
                    .Where(s => s.type == suggestType && !string.IsNullOrEmpty(s.id) &&
                                !string.Equals(s.title, seriesTitle, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault(s => s.title != null &&
                                         (s.title.Contains("季", StringComparison.Ordinal) ||
                                          s.title.Contains("Season", StringComparison.OrdinalIgnoreCase) ||
                                          s.title.Contains("Part", StringComparison.OrdinalIgnoreCase)));

                if (seasonMatch != null)
                {
                    return await FetchSubjectAsync(seasonMatch.id, cancellationToken).ConfigureAwait(false);
                }

                return null;
            }

            var imdbId = item.GetProviderId(MetadataProviders.Imdb);
            if (!string.IsNullOrEmpty(imdbId))
            {
                var doubanId = await ResolveDoubanIdByImdbAsync(imdbId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(doubanId))
                {
                    var byImdb = await FetchSubjectAsync(doubanId, cancellationToken).ConfigureAwait(false);
                    if (byImdb != null) return byImdb;

                    // IMDb 路径失败（404/风控）时回退标题搜索，而不是直接放弃
                }
            }

            var searchTerm = item.OriginalTitle ?? item.Name;
            if (string.IsNullOrWhiteSpace(searchTerm)) return null;

            return await SearchByTitleAsync(searchTerm, item.ProductionYear, suggestType, null, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 季数转中文序数（1→第一季、21→第二十一季），超出范围返回阿拉伯数字形式。
        /// </summary>
        private static string ToChineseSeasonNumber(int n)
        {
            if (n <= 0 || n > 99) return $"第{n}季";

            var digits = new[] { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

            if (n < 10) return "第" + digits[n] + "季";
            if (n == 10) return "第十季";
            if (n < 20) return "第十" + digits[n - 10] + "季";

            var tens = n / 10;
            var ones = n % 10;

            return "第" + digits[tens] + "十" + (ones > 0 ? digits[ones] : string.Empty) + "季";
        }

        private async Task<DoubanSubject> SearchByTitleAsync(string title, int? year, string suggestType,
            int? seasonNumber, CancellationToken cancellationToken)
        {
            var suggestions = await GetSuggestionsAsync(title, cancellationToken).ConfigureAwait(false);

            var matches = suggestions?
                .Where(s => s.type == suggestType && !string.IsNullOrEmpty(s.id))
                .ToList();

            var match = matches?.FirstOrDefault();

            // 季级匹配优先选标题带「季」的条目，避免命中的是剧集主条目
            if (seasonNumber.HasValue && matches != null)
            {
                match = matches.FirstOrDefault(s => s.title != null && s.title.Contains("季", StringComparison.Ordinal))
                        ?? match;
            }

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

            var query = $"SELECT ?doubanId WHERE {{ ?item wdt:P345 \"{imdbId}\" ; wdt:P4529 ?doubanId. " +
                        "FILTER(ISLITERAL(?doubanId)) }} LIMIT 1";
            var url = $"{SparqlUrl}?query={Uri.EscapeDataString(query)}&format=json";

            var json = await RequestAsync(url, cancellationToken).ConfigureAwait(false);
            if (json == null) return null;

            try
            {
                var response = _jsonSerializer.DeserializeFromString<SparqlResponse>(json);
                var doubanId = response?.results?.bindings?.FirstOrDefault()?.doubanId?.value;

                // 防御：只接受纯数字的豆瓣 subject id（过滤 blank-node/genid 等 URL 值）
                if (string.IsNullOrEmpty(doubanId) || !Regex.IsMatch(doubanId, @"^\d+$"))
                {
                    _logger.Info("DoubanAssist - Wikidata returned no valid douban id for {0}", imdbId);
                    return null;
                }

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

            // 优先走豆瓣开放平台 V2 API（若已配置 Access Token）。
            var accessToken = Plugin.Instance.MetadataEnhanceStore.GetOptions().DoubanAssist.DoubanAccessToken;
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var apiSubject = await FetchSubjectViaApiAsync(subjectId, accessToken.Trim(), cancellationToken)
                    .ConfigureAwait(false);
                if (apiSubject != null)
                {
                    SetCache(key, apiSubject);
                    if (apiSubject.Rating.HasValue)
                    {
                        RecordRatingSnapshot(subjectId, apiSubject.Rating.Value, apiSubject.Votes);
                    }

                    return apiSubject;
                }

                _logger.Info("DoubanAssist - V2 API failed for {0}, falling back to HTML scraping", subjectId);
            }

            var html = await RequestAsync($"{BaseUrl}/subject/{subjectId}/", cancellationToken).ConfigureAwait(false);
            if (html == null) return null;

            if (html.Contains("sec.douban.com", StringComparison.Ordinal) ||
                (!html.Contains("v:average", StringComparison.Ordinal) &&
                 !html.Contains("id=\"info\"", StringComparison.Ordinal)))
            {
                _logger.Info("DoubanAssist - Subject {0} blocked by douban risk control (sec page). " +
                             "Configure a logged-in DoubanCookie in settings to bypass.", subjectId);
                RegisterBlocked(subjectId);
                return null;
            }

            _consecutiveBlocked = 0;

            var subject = ParseSubject(subjectId, html);
            SetCache(key, subject);

            if (subject.Rating.HasValue)
            {
                RecordRatingSnapshot(subjectId, subject.Rating.Value, subject.Votes);
            }

            return subject;
        }

        /// <summary>
        /// 通过豆瓣开放平台 V2 API 拉取条目（需 Access Token）。
        /// 先按电影 /movie/{id} 尝试，失败再按剧集 /tv/{id} 尝试。任何异常均返回 null 以触发网页回退。
        /// </summary>
        private async Task<DoubanSubject> FetchSubjectViaApiAsync(string subjectId, string accessToken,
            CancellationToken cancellationToken)
        {
            var subject = await TryFetchV2Async(string.Format(DoubanV2MoviePath, subjectId), accessToken, cancellationToken)
                              .ConfigureAwait(false)
                          ?? await TryFetchV2Async(string.Format(DoubanV2TvPath, subjectId), accessToken, cancellationToken)
                              .ConfigureAwait(false);

            if (subject != null) subject.SubjectId = subjectId;
            return subject;
        }

        private async Task<DoubanSubject> TryFetchV2Async(string path, string accessToken,
            CancellationToken cancellationToken)
        {
            try
            {
                var requestOptions = new HttpRequestOptions
                {
                    Url = DoubanV2BaseUrl + path,
                    CancellationToken = cancellationToken,
                    AcceptHeader = "application/json",
                    BufferContent = true,
                    UserAgent = Plugin.Instance.UserAgent
                };
                requestOptions.RequestHeaders["Authorization"] = "Bearer " + accessToken;

                using var response = await _httpClient.SendAsync(requestOptions, "GET").ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Debug("DoubanAssist - V2 {0} returned {1}", path, response.StatusCode);
                    return null;
                }

                await using var contentStream = response.Content;
                var payload = _jsonSerializer.DeserializeFromStream<DoubanV2Subject>(contentStream);
                if (payload == null || string.IsNullOrEmpty(payload.id)) return null;

                var subject = new DoubanSubject
                {
                    Summary = payload.intro
                };

                if (payload.rating != null && payload.rating.value > 0)
                {
                    subject.Rating = payload.rating.value;
                }

                if (payload.rating != null && payload.rating.count > 0)
                {
                    subject.Votes = payload.rating.count;
                }

                if (payload.genres != null)
                {
                    subject.Genres = payload.genres.Where(g => !string.IsNullOrWhiteSpace(g))
                        .Select(g => g.Trim()).ToList();
                }

                if (payload.countries != null)
                {
                    subject.Countries = payload.countries.Where(c => !string.IsNullOrWhiteSpace(c))
                        .Select(c => c.Trim()).ToList();
                }

                if (payload.languages != null)
                {
                    subject.Languages = payload.languages.Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Trim()).ToList();
                }

                return subject;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.Debug("DoubanAssist - V2 request failed for {0}: {1}", path, e.Message);
                return null;
            }
        }

        private class DoubanV2Subject
        {
            public string id { get; set; }
            public string intro { get; set; }
            public DoubanV2Rating rating { get; set; }
            public List<string> genres { get; set; }
            public List<string> countries { get; set; }
            public List<string> languages { get; set; }
        }

        private class DoubanV2Rating
        {
            public double value { get; set; }
            public long count { get; set; }
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

                var cookie = Plugin.Instance.MetadataEnhanceStore.GetOptions().DoubanAssist.DoubanCookie;
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    requestOptions.RequestHeaders["Cookie"] = cookie.Trim();
                }

                using var response = await _httpClient.SendAsync(requestOptions, "GET").ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.Info("DoubanAssist - {0} returned {1}", url, response.StatusCode);
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
