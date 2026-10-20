using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace StrmAssistant.Options.Store
{
    /// <summary>
    /// 主设置（神医助手 Plus 首页）由**平铺**改为**分组嵌套**：
    /// GeneralOptions 拆为任务与并发 / 内存管理 / 运行状态与维护，
    /// ModOptions 拆为中文搜索 / 直链重定向（直链原本就是子组）。
    /// 迁移完成后：旧内容备份为 <c>*.legacy.bak</c>，并写入 flag 文件避免重复迁移。
    /// </summary>
    /// <remarks>
    /// 「显示媒体信息」（ShowMediaInfo）已从 ModOptions 迁到体验增强的界面功能组，
    /// 两者不在同一个配置文件，迁移时无法跨文件搬运，该开关会回退为默认值 OFF。
    /// </remarks>
    internal static class PluginOptionsMigration
    {
        private const string FlagFileName = "StrmAssistant_PluginOptions_grouped_v1.flag";

        private static readonly string[] LegacyKeys =
        {
            "CatchupMode", "EnableMemoryAutoRestart", "EnableMemoryCleanup", "MaxConcurrentCount",
            "EnhanceChineseSearch", "ShowMediaInfo"
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

                var legacy = jsonSerializer.DeserializeFromString<LegacyPluginOptions>(legacyText);
                var migrated = new PluginOptions();
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
                    "PluginOptions: migrated legacy flat layout to grouped layout. Legacy backup: {0}", backupPath);
            }
            catch (Exception e)
            {
                logger.Warn("PluginOptions legacy migration failed: " + e.Message);
            }
        }

        private static bool IsGroupedLayout(string text)
        {
            return text.Contains("\"TaskConcurrency\"") || text.Contains("\"ChineseSearch\"");
        }

        private static bool HasLegacyKeys(string text)
        {
            foreach (var key in LegacyKeys)
            {
                if (text.Contains("\"" + key + "\"")) return true;
            }

            return false;
        }

        private static void MapLegacyValues(LegacyPluginOptions legacy, PluginOptions options)
        {
            var task = options.GeneralOptions.TaskConcurrency;
            task.CatchupMode = legacy.GeneralOptions.CatchupMode;
            // 旧版是分钟粒度；新版改为秒，换算后 minutes 置 0（避免重复换算）
            task.CatchupMinWaitSeconds = Math.Min(7200, Math.Max(0, legacy.GeneralOptions.CatchupMinWaitMinutes) * 60);
            task.CatchupMinWaitMinutes = 0;
            task.CatchupTaskScope = string.IsNullOrEmpty(legacy.GeneralOptions.CatchupTaskScope)
                ? TaskConcurrencyOptions.CatchupTask.MediaInfo.ToString()
                : legacy.GeneralOptions.CatchupTaskScope;
            task.MaxConcurrentCount = legacy.GeneralOptions.MaxConcurrentCount;
            task.CooldownDurationSeconds = legacy.GeneralOptions.CooldownDurationSeconds;
            task.Tier2MaxConcurrentCount = legacy.GeneralOptions.Tier2MaxConcurrentCount;

            var memory = options.GeneralOptions.Memory;
            memory.EnableMemoryAutoRestart = legacy.GeneralOptions.EnableMemoryAutoRestart;
            memory.MemoryAutoRestartThresholdMB = legacy.GeneralOptions.MemoryAutoRestartThresholdMB;
            memory.EnableMemoryCleanup = legacy.GeneralOptions.EnableMemoryCleanup;
            memory.MemoryCleanupIntervalMinutes = legacy.GeneralOptions.MemoryCleanupIntervalMinutes;

            var search = options.ModOptions.ChineseSearch;
            search.EnhanceChineseSearch = legacy.ModOptions.EnhanceChineseSearch;
            search.EnhanceChineseSearchRestore = legacy.ModOptions.EnhanceChineseSearchRestore;
            search.SearchScope = string.IsNullOrEmpty(legacy.ModOptions.SearchScope)
                ? string.Join(",", new[]
                {
                    ChineseSearchOptions.SearchItemType.Movie, ChineseSearchOptions.SearchItemType.Collection,
                    ChineseSearchOptions.SearchItemType.Series
                })
                : legacy.ModOptions.SearchScope;
            search.ExcludeOriginalTitleFromSearch = legacy.ModOptions.ExcludeOriginalTitleFromSearch;
            search.DigitsAsTmdbId = legacy.ModOptions.DigitsAsTmdbId;
            search.MixedChineseVariants = legacy.ModOptions.MixedChineseVariants;

            if (legacy.AboutOptions != null) options.AboutOptions = legacy.AboutOptions;
        }

        /// <summary>旧版平铺配置 DTO，仅用于一次性迁移读取。</summary>
        private class LegacyPluginOptions
        {
            public LegacyGeneralOptions GeneralOptions { get; set; } = new LegacyGeneralOptions();
            public LegacyModOptions ModOptions { get; set; } = new LegacyModOptions();
            public NetworkOptions NetworkOptions { get; set; } = new NetworkOptions();
            public AboutOptions AboutOptions { get; set; } = new AboutOptions();
        }

        private class LegacyGeneralOptions
        {
            public bool CatchupMode { get; set; } = false;
            public int CatchupMinWaitMinutes { get; set; } = 0;
            public string CatchupTaskScope { get; set; } = "MediaInfo";
            public int MaxConcurrentCount { get; set; } = 1;
            public int CooldownDurationSeconds { get; set; } = 0;
            public int Tier2MaxConcurrentCount { get; set; } = 1;
            public bool EnableMemoryAutoRestart { get; set; } = false;
            public int MemoryAutoRestartThresholdMB { get; set; } = 4096;
            public bool EnableMemoryCleanup { get; set; } = true;
            public int MemoryCleanupIntervalMinutes { get; set; } = 5;
        }

        private class LegacyModOptions
        {
            public bool EnhanceChineseSearch { get; set; } = false;
            public bool EnhanceChineseSearchRestore { get; set; } = false;
            public string SearchScope { get; set; } =
                string.Join(",", new[]
                {
                    ChineseSearchOptions.SearchItemType.Movie, ChineseSearchOptions.SearchItemType.Collection,
                    ChineseSearchOptions.SearchItemType.Series
                });
            public bool ExcludeOriginalTitleFromSearch { get; set; } = false;
            public bool DigitsAsTmdbId { get; set; } = false;
            public bool MixedChineseVariants { get; set; } = false;
            public bool ShowMediaInfo { get; set; } = false;
            public DirectLinkOptions DirectLinkOptions { get; set; } = new DirectLinkOptions();
        }
    }
}
