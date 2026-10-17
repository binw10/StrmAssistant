using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace StrmAssistant.Options.Store
{
    /// <summary>
    /// 「体验增强」设置由**平铺**改为**分组嵌套**（多版本合并 / 多版本展示 / 播放与清理 / 通知）。
    /// 「界面功能」子组原本就是嵌套对象，迁移时原样保留。
    /// 迁移完成后：旧内容备份为 <c>*.legacy.bak</c>，并写入 flag 文件避免重复迁移。
    /// </summary>
    internal static class ExperienceEnhanceOptionsMigration
    {
        private const string FlagFileName = "StrmAssistant_ExperienceEnhanceOptions_grouped_v1.flag";

        private static readonly (string Group, string[] Keys)[] Layout =
        {
            (nameof(ExperienceEnhanceOptions.Merge), new[]
            {
                "MergeMultiVersion", "MergeMoviesPreference", "MergeSeriesPreference",
                "FolderVideoGroupingLimit"
            }),
            (nameof(ExperienceEnhanceOptions.Display), new[]
            {
                "EnableMultiVersionEnhance", "MultiVersionDisplayOption", "MultiVersionSortOption"
            }),
            (nameof(ExperienceEnhanceOptions.Playback), new[]
            {
                "EnableAutoMarkWatched", "AutoMarkWatchedThreshold", "AutoMarkWatchedExcludeFavorites",
                "EnableDeepDelete", "OptimizeSubtitle"
            }),
            (nameof(ExperienceEnhanceOptions.Notification), new[]
            {
                "EnhanceNotificationSystem", "MetadataUpdateTrackedFields"
            })
        };

        private static readonly string[] GroupKeys =
        {
            nameof(ExperienceEnhanceOptions.Merge),
            nameof(ExperienceEnhanceOptions.Display),
            nameof(ExperienceEnhanceOptions.Playback),
            nameof(ExperienceEnhanceOptions.Notification)
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

                var legacy = jsonSerializer.DeserializeFromString<LegacyExperienceEnhanceOptions>(legacyText);
                var migrated = new ExperienceEnhanceOptions();
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
                    "ExperienceEnhanceOptions: migrated legacy flat layout to grouped layout (4 groups). Legacy backup: {0}",
                    backupPath);
            }
            catch (Exception e)
            {
                logger.Warn("ExperienceEnhanceOptions legacy migration failed: " + e.Message);
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

        private static void MapLegacyValues(LegacyExperienceEnhanceOptions legacy, ExperienceEnhanceOptions options)
        {
            var merge = options.Merge;
            merge.MergeMultiVersion = legacy.MergeMultiVersion;
            merge.FolderVideoGroupingLimit = legacy.FolderVideoGroupingLimit;

            if (Enum.TryParse<VersionMergeOptions.MergeMoviesScopeOption>(legacy.MergeMoviesPreference, true,
                    out var moviesPreference))
            {
                merge.MergeMoviesPreference = moviesPreference;
            }

            if (Enum.TryParse<VersionMergeOptions.MergeSeriesScopeOption>(legacy.MergeSeriesPreference, true,
                    out var seriesPreference))
            {
                merge.MergeSeriesPreference = seriesPreference;
            }

            var display = options.Display;
            display.EnableMultiVersionEnhance = legacy.EnableMultiVersionEnhance;

            if (Enum.TryParse<MultipleVersionsOptions.MultiVersionDisplayOption>(legacy.MultiVersionDisplayOption, true,
                    out var displayOption))
            {
                display.MultiVersionDisplayOption = displayOption;
            }

            if (Enum.TryParse<MultipleVersionsOptions.MultiVersionSortOption>(legacy.MultiVersionSortOption, true,
                    out var sortOption))
            {
                display.MultiVersionSortOption = sortOption;
            }

            var playback = options.Playback;
            playback.EnableAutoMarkWatched = legacy.EnableAutoMarkWatched;
            playback.AutoMarkWatchedThreshold = legacy.AutoMarkWatchedThreshold;
            playback.AutoMarkWatchedExcludeFavorites = legacy.AutoMarkWatchedExcludeFavorites;
            playback.EnableDeepDelete = legacy.EnableDeepDelete;
            playback.OptimizeSubtitle = legacy.OptimizeSubtitle;

            var notification = options.Notification;
            notification.EnhanceNotificationSystem = legacy.EnhanceNotificationSystem;
            notification.MetadataUpdateTrackedFields = legacy.MetadataUpdateTrackedFields ?? string.Empty;

            // 「界面功能」原本就是嵌套对象，原样沿用
            if (legacy.UIFunctionOptions != null) options.UIFunctionOptions = legacy.UIFunctionOptions;
        }

        /// <summary>
        /// 旧版的平铺配置 DTO，仅用于一次性迁移读取。
        /// 枚举按字符串读入再 TryParse，兼容历史配置里可能出现的数字/大小写差异。
        /// </summary>
        private class LegacyExperienceEnhanceOptions
        {
            public bool MergeMultiVersion { get; set; } = false;
            public string MergeMoviesPreference { get; set; } = "FolderScope";
            public string MergeSeriesPreference { get; set; } = "LibraryScope";
            public int FolderVideoGroupingLimit { get; set; } = 8;

            public bool EnableMultiVersionEnhance { get; set; } = false;
            public string MultiVersionDisplayOption { get; set; } = "MediaInfo";
            public string MultiVersionSortOption { get; set; } = "SystemDefault";

            public bool EnableAutoMarkWatched { get; set; } = false;
            public int AutoMarkWatchedThreshold { get; set; } = 90;
            public bool AutoMarkWatchedExcludeFavorites { get; set; } = false;
            public bool EnableDeepDelete { get; set; } = false;
            public bool OptimizeSubtitle { get; set; } = false;

            public bool EnhanceNotificationSystem { get; set; } = false;
            public string MetadataUpdateTrackedFields { get; set; } = string.Empty;

            public UIFunctionOptions UIFunctionOptions { get; set; } = new UIFunctionOptions();
        }
    }
}
