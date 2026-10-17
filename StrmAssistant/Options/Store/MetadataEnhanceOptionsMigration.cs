using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace StrmAssistant.Options.Store
{
    /// <summary>
    /// 3.0.0.55 起「元数据增强」设置由**平铺**改为**分组嵌套**（9 个分组小标题）。
    /// 本类负责把旧版平铺 JSON 就地迁移为嵌套结构，避免用户已配置的选项被重置为默认值。
    /// 迁移完成后：旧内容备份为 <c>*.legacy.bak</c>，并写入 flag 文件避免重复迁移。
    /// </summary>
    internal static class MetadataEnhanceOptionsMigration
    {
        private const string FlagFileName = "StrmAssistant_MetadataEnhanceOptions_migrated_3_0_0_55.flag";

        /// <summary>
        /// 旧字段 → 新分组 的映射表（Group 取新分组属性名，用 nameof 保证编译期同步）。
        /// </summary>
        private static readonly (string Group, string[] Keys)[] Layout =
        {
            (nameof(MetadataEnhanceOptions.ChineseMetadata), new[]
            {
                "ChineseMovieDb", "FallbackLanguages", "ChineseTvdb", "TvdbFallbackLanguages",
                "BlockNonFallbackLanguage", "PreferTraditionalChinese", "DefaultNewLibraryTmdb"
            }),
            (nameof(MetadataEnhanceOptions.EpisodeGroup), new[]
            {
                "MovieDbEpisodeGroup", "LocalEpisodeGroup", "EnableMovieDbEpisodeGroupExtendedInfo"
            }),
            (nameof(MetadataEnhanceOptions.ScrapeOptimize), new[]
            {
                "OptimizeMovieDbEpisodeScraping", "DisableMovieDbEpisodeScrapingFallback", "DisableEpisodeImageScraping"
            }),
            (nameof(MetadataEnhanceOptions.PersonEnhance), new[]
            {
                "EnhanceMovieDbPerson", "RefreshPersonMode"
            }),
            (nameof(MetadataEnhanceOptions.DoubanAssist), new[]
            {
                "EnableDoubanAssist", "DoubanCookie", "DoubanAccessToken", "DoubanRatingOverride",
                "DoubanAssistScrapeScope", "MdbListApiKey", "RatingUpdateLookbackDays",
                "DoubanCastChinese", "DoubanPosterFallback"
            }),
            (nameof(MetadataEnhanceOptions.PosterSort), new[]
            {
                "PreferOriginalPoster", "PinyinSortName", "EnhanceNfoMetadata", "ClearTagline"
            }),
            (nameof(MetadataEnhanceOptions.MetadataRefresh), new[]
            {
                "EpisodeRefreshScope", "EpisodeRefreshLookbackDays", "EpisodeRefreshLibraryScope"
            }),
            (nameof(MetadataEnhanceOptions.MetadataCache), new[]
            {
                "MetadataCacheMode", "MetadataCacheRootFolder", "MetadataCacheOptionalImageTypes"
            })
        };

        /// <summary>新结构才会出现的根级分组键（用于判断文件是否已经是嵌套格式）。</summary>
        private static readonly string[] GroupKeys =
        {
            nameof(MetadataEnhanceOptions.ChineseMetadata),
            nameof(MetadataEnhanceOptions.EpisodeGroup),
            nameof(MetadataEnhanceOptions.ScrapeOptimize),
            nameof(MetadataEnhanceOptions.PersonEnhance),
            nameof(MetadataEnhanceOptions.DoubanAssist),
            nameof(MetadataEnhanceOptions.PosterSort),
            nameof(MetadataEnhanceOptions.MetadataRefresh),
            nameof(MetadataEnhanceOptions.MetadataCache)
        };

        public static void MigrateIfNeeded(string optionsFilePath, IJsonSerializer jsonSerializer, ILogger logger)
        {
            try
            {
                var directory = Path.GetDirectoryName(optionsFilePath);

                if (string.IsNullOrEmpty(directory)) return;

                var flagPath = Path.Combine(directory, FlagFileName);

                if (File.Exists(flagPath) || !File.Exists(optionsFilePath)) return;

                var legacyText = File.ReadAllText(optionsFilePath);

                if (IsGroupedLayout(legacyText) || !HasLegacyKeys(legacyText))
                {
                    File.WriteAllText(flagPath, DateTime.UtcNow.ToString("O"));
                    return;
                }

                var legacy = jsonSerializer.DeserializeFromString<LegacyMetadataEnhanceOptions>(legacyText);
                var migrated = new MetadataEnhanceOptions();
                MapLegacyValues(legacy, migrated);

                // 先在内存中完成序列化，避免写文件中途失败损坏配置
                byte[] bytes;

                using (var stream = new MemoryStream())
                {
                    jsonSerializer.SerializeToStream(migrated, stream, new JsonSerializerOptions { Indent = true });
                    bytes = stream.ToArray();
                }

                var backupPath = optionsFilePath + ".legacy.bak";
                File.WriteAllText(backupPath, legacyText);
                File.WriteAllBytes(optionsFilePath, bytes);
                File.WriteAllText(flagPath, DateTime.UtcNow.ToString("O"));

                logger.Info(
                    "MetadataEnhanceOptions: migrated legacy flat layout to grouped layout (9 groups). Legacy backup: {0}",
                    backupPath);
            }
            catch (Exception e)
            {
                logger.Warn("MetadataEnhanceOptions legacy migration failed: " + e.Message);
            }
        }

        private static bool IsGroupedLayout(string text)
        {
            foreach (var key in GroupKeys)
            {
                if (text.Contains("\"" + key + "\"")) return true;
            }

            return false;
        }

        private static bool HasLegacyKeys(string text)
        {
            foreach (var (_, keys) in Layout)
            {
                foreach (var key in keys)
                {
                    if (text.Contains("\"" + key + "\"")) return true;
                }
            }

            return false;
        }

        private static void MapLegacyValues(LegacyMetadataEnhanceOptions legacy, MetadataEnhanceOptions options)
        {
            var chinese = options.ChineseMetadata;
            chinese.ChineseMovieDb = legacy.ChineseMovieDb;
            chinese.FallbackLanguages = legacy.FallbackLanguages;
            chinese.ChineseTvdb = legacy.ChineseTvdb;
            chinese.TvdbFallbackLanguages = legacy.TvdbFallbackLanguages;
            chinese.BlockNonFallbackLanguage = legacy.BlockNonFallbackLanguage;
            chinese.PreferTraditionalChinese = legacy.PreferTraditionalChinese;
            chinese.DefaultNewLibraryTmdb = legacy.DefaultNewLibraryTmdb;

            var episodeGroup = options.EpisodeGroup;
            episodeGroup.MovieDbEpisodeGroup = legacy.MovieDbEpisodeGroup;
            episodeGroup.LocalEpisodeGroup = legacy.LocalEpisodeGroup;
            episodeGroup.EnableMovieDbEpisodeGroupExtendedInfo = legacy.EnableMovieDbEpisodeGroupExtendedInfo;

            var scrapeOptimize = options.ScrapeOptimize;
            scrapeOptimize.OptimizeMovieDbEpisodeScraping = legacy.OptimizeMovieDbEpisodeScraping;
            scrapeOptimize.DisableMovieDbEpisodeScrapingFallback = legacy.DisableMovieDbEpisodeScrapingFallback;
            scrapeOptimize.DisableEpisodeImageScraping = legacy.DisableEpisodeImageScraping;

            var personEnhance = options.PersonEnhance;
            personEnhance.EnhanceMovieDbPerson = legacy.EnhanceMovieDbPerson;
            personEnhance.RefreshPersonMode = legacy.RefreshPersonMode;

            var douban = options.DoubanAssist;
            douban.EnableDoubanAssist = legacy.EnableDoubanAssist;
            douban.DoubanCookie = legacy.DoubanCookie;
            douban.DoubanAccessToken = legacy.DoubanAccessToken;
            douban.DoubanRatingOverride = legacy.DoubanRatingOverride;
            douban.DoubanAssistScrapeScope = legacy.DoubanAssistScrapeScope;
            douban.MdbListApiKey = legacy.MdbListApiKey;
            douban.RatingUpdateLookbackDays = legacy.RatingUpdateLookbackDays;
            douban.DoubanCastChinese = legacy.DoubanCastChinese;
            douban.DoubanPosterFallback = legacy.DoubanPosterFallback;

            var posterSort = options.PosterSort;
            posterSort.PreferOriginalPoster = legacy.PreferOriginalPoster;
            posterSort.PinyinSortName = legacy.PinyinSortName;
            posterSort.EnhanceNfoMetadata = legacy.EnhanceNfoMetadata;
            posterSort.ClearTagline = legacy.ClearTagline;

            var refresh = options.MetadataRefresh;
            refresh.EpisodeRefreshScope = legacy.EpisodeRefreshScope;
            refresh.EpisodeRefreshLookbackDays = legacy.EpisodeRefreshLookbackDays;
            refresh.EpisodeRefreshLibraryScope = legacy.EpisodeRefreshLibraryScope;

            var cache = options.MetadataCache;
            cache.MetadataCacheMode = legacy.MetadataCacheMode;
            cache.MetadataCacheRootFolder = legacy.MetadataCacheRootFolder;
            cache.MetadataCacheOptionalImageTypes = legacy.MetadataCacheOptionalImageTypes;
        }

        /// <summary>
        /// 旧版（3.0.55 及以前）的平铺配置 DTO，仅用于一次性迁移读取。
        /// </summary>
        private class LegacyMetadataEnhanceOptions
        {
            public bool ChineseMovieDb { get; set; }
            public string FallbackLanguages { get; set; }
            public bool ChineseTvdb { get; set; }
            public string TvdbFallbackLanguages { get; set; }
            public bool BlockNonFallbackLanguage { get; set; }
            public bool PreferTraditionalChinese { get; set; }
            public bool DefaultNewLibraryTmdb { get; set; }

            public bool MovieDbEpisodeGroup { get; set; }
            public bool LocalEpisodeGroup { get; set; }
            public bool EnableMovieDbEpisodeGroupExtendedInfo { get; set; }

            public bool OptimizeMovieDbEpisodeScraping { get; set; }
            public bool DisableMovieDbEpisodeScrapingFallback { get; set; }
            public bool DisableEpisodeImageScraping { get; set; }

            public bool EnhanceMovieDbPerson { get; set; }
            public string RefreshPersonMode { get; set; }

            public bool EnableDoubanAssist { get; set; }
            public string DoubanCookie { get; set; }
            public string DoubanAccessToken { get; set; }
            public bool DoubanRatingOverride { get; set; }
            public string DoubanAssistScrapeScope { get; set; }
            public string MdbListApiKey { get; set; }
            public int RatingUpdateLookbackDays { get; set; }
            public bool DoubanCastChinese { get; set; }
            public bool DoubanPosterFallback { get; set; }

            public bool PreferOriginalPoster { get; set; }
            public bool PinyinSortName { get; set; }
            public bool EnhanceNfoMetadata { get; set; }
            public bool ClearTagline { get; set; }

            public string EpisodeRefreshScope { get; set; }
            public int EpisodeRefreshLookbackDays { get; set; }
            public string EpisodeRefreshLibraryScope { get; set; }

            public string MetadataCacheMode { get; set; }
            public string MetadataCacheRootFolder { get; set; }
            public string MetadataCacheOptionalImageTypes { get; set; }

            public bool AltMovieDbConfig { get; set; }
            public string AltMovieDbApiUrl { get; set; }
            public string AltMovieDbImageUrl { get; set; }
            public string AltMovieDbApiKey { get; set; }
        }
    }
}
