using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace StrmAssistant.Options.Store
{
    /// <summary>
    /// 「片头片尾探测」设置由**平铺**改为**分组嵌套**（3 个分组：检测与指纹 / 在线刮削 / 播放与跳过）。
    /// 本类负责把旧版平铺 JSON 就地迁移为嵌套结构，避免用户已配置的选项被重置为默认值。
    /// 迁移完成后：旧内容备份为 <c>*.legacy.bak</c>，并写入 flag 文件避免重复迁移。
    /// </summary>
    internal static class IntroSkipOptionsMigration
    {
        private const string FlagFileName = "StrmAssistant_IntroSkipOptions_grouped_v1.flag";

        private static readonly (string Group, string[] Keys)[] Layout =
        {
            (nameof(IntroSkipOptions.Fingerprint), new[]
            {
                "UnlockIntroSkip", "IntroDetectionFingerprintMinutes", "LibraryFingerprintMinutes",
                "ParallelMarkerComputation", "MarkerEnabledLibraryScope",
                "FingerprintBlacklistShows", "FingerprintBlacklistKeywords"
            }),
            (nameof(IntroSkipOptions.Scrape), new[]
            {
                "EnableIntroDbScrape", "EnableAutomaticIntroScraping", "IntroDbApiKey", "TidbApiKey",
                "UnifiedIntroDbProviders"
            }),
            (nameof(IntroSkipOptions.Playback), new[]
            {
                "EnableIntroSkip", "MaxIntroDurationSeconds", "MaxCreditsDurationSeconds",
                "MinOpeningPlotDurationSeconds", "LibraryScope", "UserScope", "ClientScope",
                "IntroSkipPreferences"
            })
        };

        private static readonly string[] GroupKeys =
        {
            nameof(IntroSkipOptions.Fingerprint),
            nameof(IntroSkipOptions.Scrape),
            nameof(IntroSkipOptions.Playback)
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

                var legacy = jsonSerializer.DeserializeFromString<LegacyIntroSkipOptions>(legacyText);
                var migrated = new IntroSkipOptions();
                MapLegacyValues(legacy, migrated);

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
                    "IntroSkipOptions: migrated legacy flat layout to grouped layout (3 groups). Legacy backup: {0}",
                    backupPath);
            }
            catch (Exception e)
            {
                logger.Warn("IntroSkipOptions legacy migration failed: " + e.Message);
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

        private static void MapLegacyValues(LegacyIntroSkipOptions legacy, IntroSkipOptions options)
        {
            var fingerprint = options.Fingerprint;
            fingerprint.UnlockIntroSkip = legacy.UnlockIntroSkip;
            fingerprint.IntroDetectionFingerprintMinutes = legacy.IntroDetectionFingerprintMinutes;
            fingerprint.LibraryFingerprintMinutes = legacy.LibraryFingerprintMinutes ?? string.Empty;
            fingerprint.ParallelMarkerComputation = legacy.ParallelMarkerComputation;
            fingerprint.MarkerEnabledLibraryScope = legacy.MarkerEnabledLibraryScope ?? string.Empty;
            fingerprint.FingerprintBlacklistShows = legacy.FingerprintBlacklistShows ?? string.Empty;
            fingerprint.FingerprintBlacklistKeywords = legacy.FingerprintBlacklistKeywords ?? string.Empty;

            var scrape = options.Scrape;
            scrape.EnableIntroDbScrape = legacy.EnableIntroDbScrape;
            scrape.EnableAutomaticIntroScraping = legacy.EnableAutomaticIntroScraping;
            scrape.IntroDbApiKey = legacy.IntroDbApiKey ?? string.Empty;
            scrape.TidbApiKey = legacy.TidbApiKey ?? string.Empty;
            scrape.UnifiedIntroDbProviders = string.IsNullOrEmpty(legacy.UnifiedIntroDbProviders)
                ? "IntroDb,Tidb"
                : legacy.UnifiedIntroDbProviders;

            var playback = options.Playback;
            playback.EnableIntroSkip = legacy.EnableIntroSkip;
            playback.MaxIntroDurationSeconds = legacy.MaxIntroDurationSeconds;
            playback.MaxCreditsDurationSeconds = legacy.MaxCreditsDurationSeconds;
            playback.MinOpeningPlotDurationSeconds = legacy.MinOpeningPlotDurationSeconds;
            playback.LibraryScope = legacy.LibraryScope ?? string.Empty;
            playback.UserScope = legacy.UserScope ?? string.Empty;
            playback.ClientScope = string.IsNullOrEmpty(legacy.ClientScope)
                ? "Emby,Infuse,SenPlayer"
                : legacy.ClientScope;
            playback.IntroSkipPreferences = legacy.IntroSkipPreferences ?? string.Empty;
        }

        /// <summary>
        /// 旧版的平铺配置 DTO，仅用于一次性迁移读取。
        /// 属性默认值与旧版选项默认值保持一致，避免旧 JSON 缺键时行为漂移。
        /// </summary>
        private class LegacyIntroSkipOptions
        {
            public bool UnlockIntroSkip { get; set; } = false;
            public int IntroDetectionFingerprintMinutes { get; set; } = 10;
            public string LibraryFingerprintMinutes { get; set; } = string.Empty;
            public bool ParallelMarkerComputation { get; set; } = false;
            public string MarkerEnabledLibraryScope { get; set; } = string.Empty;
            public string FingerprintBlacklistShows { get; set; } = string.Empty;
            public string FingerprintBlacklistKeywords { get; set; } = string.Empty;

            public bool EnableIntroDbScrape { get; set; } = false;
            public bool EnableAutomaticIntroScraping { get; set; } = false;
            public string IntroDbApiKey { get; set; } = string.Empty;
            public string TidbApiKey { get; set; } = string.Empty;
            public string UnifiedIntroDbProviders { get; set; } = "IntroDb,Tidb";

            public bool EnableIntroSkip { get; set; } = false;
            public int MaxIntroDurationSeconds { get; set; } = 150;
            public int MaxCreditsDurationSeconds { get; set; } = 360;
            public int MinOpeningPlotDurationSeconds { get; set; } = 60;
            public string LibraryScope { get; set; } = string.Empty;
            public string UserScope { get; set; } = string.Empty;
            public string ClientScope { get; set; } = "Emby,Infuse,SenPlayer";
            public string IntroSkipPreferences { get; set; } = string.Empty;
        }
    }
}
