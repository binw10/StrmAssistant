using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.PropertyDiff;
using MediaBrowser.Common;
using MediaBrowser.Model.Logging;
using StrmAssistant.Common;
using StrmAssistant.Core;
using StrmAssistant.Mod;
using StrmAssistant.Options.UIBaseClasses.Store;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using static StrmAssistant.Common.CommonUtility;
using static StrmAssistant.Options.TaskConcurrencyOptions;
using static StrmAssistant.Options.Utility;

namespace StrmAssistant.Options.Store
{
    public class PluginOptionsStore : SimpleFileStore<PluginOptions>
    {
        private readonly ILogger _logger;

        private bool _currentSuppressOnOptionsSaved;

        public PluginOptionsStore(IApplicationHost applicationHost, ILogger logger, string pluginFullName)
            : base(applicationHost, logger, pluginFullName)
        {
            _logger = logger;

            // 分组化改造：GeneralOptions / ModOptions 由平铺改为分组嵌套。先把旧配置就地迁移，
            // 否则旧键会被反序列化忽略、用户设置静默回退为默认值。
            PluginOptionsMigration.MigrateIfNeeded(OptionsFilePath,
                applicationHost.Resolve<MediaBrowser.Model.Serialization.IJsonSerializer>(), logger);

            FileSaved += OnFileSaved;
            FileSaving += OnFileSaving;
        }

        public PluginOptions PluginOptions => GetOptions();

        public void SavePluginOptionsSuppress()
        {
            _currentSuppressOnOptionsSaved = true;
            SetOptions(PluginOptions);
        }
        
        private void OnFileSaving(object sender, FileSavingEventArgs e)
        {
            if (e.Options is PluginOptions options)
            {
                var suppress = _currentSuppressOnOptionsSaved;

                if (string.IsNullOrEmpty(options.GeneralOptions.TaskConcurrency.CatchupTaskScope))
                {
                    options.GeneralOptions.TaskConcurrency.CatchupTaskScope = CatchupTask.MediaInfo.ToString();
                }
                else if (options.GeneralOptions.TaskConcurrency.CatchupMode &&
                         !Plugin.Instance.IntroSkipStore.GetOptions().Fingerprint.UnlockIntroSkip)
                {
                    var taskScope = options.GeneralOptions.TaskConcurrency.CatchupTaskScope;
                    var selectedTasks = taskScope.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(f => f != CatchupTask.Fingerprint.ToString())
                        .ToList();
                    options.GeneralOptions.TaskConcurrency.CatchupTaskScope = string.Join(",", selectedTasks);
                }

                var changes = PropertyChangeDetector.DetectObjectPropertyChanges(PluginOptions, options);
                var changedProperties = new HashSet<string>(changes.Select(c => c.PropertyName));

                // 插件总开关需重启生效：保存时立即提示管理员重启，
                // 避免「点了关闭但功能还在跑」的困惑（Emby 顶部会弹出重启提示）。
                if (changedProperties.Contains(nameof(PluginOptions.EnablePlugin)))
                {
                    _logger.Info("EnablePlugin changed to {0} - a server restart is required to take effect",
                        options.EnablePlugin);
                    Plugin.Instance.ApplicationHost.NotifyPendingRestart();
                }

                if (changedProperties.Contains(nameof(PluginOptions.GeneralOptions.TaskConcurrency.CatchupMode)))
                {
                    if (options.GeneralOptions.TaskConcurrency.CatchupMode)
                    {
                        QueueManager.Initialize();
                    }
                    else
                    {
                        QueueManager.Dispose();
                    }
                }

                if (changedProperties.Contains(nameof(PluginOptions.GeneralOptions.TaskConcurrency.CatchupTaskScope)))
                {
                    UpdateCatchupScope(options.GeneralOptions.TaskConcurrency.CatchupTaskScope);
                }

                if (changedProperties.Contains(nameof(PluginOptions.GeneralOptions.TaskConcurrency.MaxConcurrentCount)))
                {
                    var maxConcurrentCount = options.GeneralOptions.TaskConcurrency.MaxConcurrentCount;

                    QueueManager.UpdateMasterSemaphore(maxConcurrentCount);

                    if (Plugin.Instance.MediaInfoExtractStore.GetOptions().ImageCapture.EnableImageCapture)
                        EnableImageCapture.UpdateResourcePool(maxConcurrentCount);

                    Plugin.FingerprintApi.PatchTimeout(maxConcurrentCount);
                }

                if (changedProperties.Contains(nameof(PluginOptions.GeneralOptions.TaskConcurrency.Tier2MaxConcurrentCount)))
                {
                    QueueManager.UpdateTier2Semaphore(options.GeneralOptions.TaskConcurrency.Tier2MaxConcurrentCount);
                }

                if (changedProperties.Contains(nameof(PluginOptions.GeneralOptions.Memory.EnableMemoryCleanup)) ||
                    changedProperties.Contains(nameof(PluginOptions.GeneralOptions.Memory.MemoryCleanupIntervalMinutes)))
                {
                    MemoryCleaner.ApplySettings(_logger,
                        options.GeneralOptions.Memory.EnableMemoryCleanup,
                        options.GeneralOptions.Memory.MemoryCleanupIntervalMinutes);
                }

                if (changedProperties.Contains(nameof(PluginOptions.GeneralOptions.Memory.EnableMemoryAutoRestart)) ||
                    changedProperties.Contains(nameof(PluginOptions.GeneralOptions.Memory.MemoryAutoRestartThresholdMB)))
                {
                    MemoryGuard.Apply(_logger,
                        options.GeneralOptions.Memory.EnableMemoryAutoRestart,
                        options.GeneralOptions.Memory.MemoryAutoRestartThresholdMB);
                }

                if (PatchManager.EnhanceChineseSearch != null)
                {
                    var isSimpleTokenizer = string.Equals(EnhanceChineseSearch.CurrentTokenizerName, "simple",
                        StringComparison.Ordinal);
                    options.ModOptions.ChineseSearch.EnhanceChineseSearchRestore =
                        !options.ModOptions.ChineseSearch.EnhanceChineseSearch && isSimpleTokenizer;

                    if (changedProperties.Contains(nameof(ChineseSearchOptions.EnhanceChineseSearch)) &&
                        ((!options.ModOptions.ChineseSearch.EnhanceChineseSearch && isSimpleTokenizer) ||
                         (options.ModOptions.ChineseSearch.EnhanceChineseSearch && !isSimpleTokenizer)))
                    {
                        Plugin.Instance.ApplicationHost.NotifyPendingRestart();
                    }
                }

                if (changedProperties.Contains(nameof(ChineseSearchOptions.SearchScope)))
                {
                    UpdateSearchScope(options.ModOptions.ChineseSearch.SearchScope);
                }

                // MixedChineseVariants 改变了索引侧的归一化规则，切换时必须重建 FTS 索引，
                // 否则旧索引与新查询规则不一致会导致互通匹配失效。
                if (changedProperties.Contains(nameof(ChineseSearchOptions.MixedChineseVariants)))
                {
                    _logger.Info("MixedChineseVariants is set to {0}", options.ModOptions.ChineseSearch.MixedChineseVariants);
                    EnhanceChineseSearch.ScheduleIndexRebuild();
                    Plugin.Instance.ApplicationHost.NotifyPendingRestart();
                }

                if (changedProperties.Contains(nameof(ChineseSearchOptions.DigitsAsTmdbId)))
                {
                    _logger.Info("DigitsAsTmdbId is set to {0}", options.ModOptions.ChineseSearch.DigitsAsTmdbId);
                }
            }
        }

        private void OnFileSaved(object sender, FileSavedEventArgs e)
        {
            if (e.Options is PluginOptions options)
            {
                var suppress = _currentSuppressOnOptionsSaved;

                if (!suppress)
                {
                    _logger.Info("CatchupMode is set to {0}", options.GeneralOptions.TaskConcurrency.CatchupMode);
                    var catchupTaskScope = GetSelectedCatchupTaskDescription();
                    _logger.Info("CatchupTaskScope is set to {0}",
                        string.IsNullOrEmpty(catchupTaskScope) ? "EMPTY" : catchupTaskScope);
                    _logger.Info("CatchupMinWaitSeconds is set to {0}",
                        options.GeneralOptions.TaskConcurrency.EffectiveCatchupMinWaitSeconds);
                    _logger.Info("EnableMemoryAutoRestart is set to {0}", options.GeneralOptions.Memory.EnableMemoryAutoRestart);
                    _logger.Info("MemoryAutoRestartThresholdMB is set to {0}", options.GeneralOptions.Memory.MemoryAutoRestartThresholdMB);

                    _logger.Info("Master MaxConcurrentCount is set to {0}", options.GeneralOptions.TaskConcurrency.MaxConcurrentCount);
                    _logger.Info("CooldownDurationSeconds is set to {0}", options.GeneralOptions.TaskConcurrency.CooldownDurationSeconds);
                    _logger.Info("Tier2 MaxConcurrentCount is set to {0}", options.GeneralOptions.TaskConcurrency.Tier2MaxConcurrentCount);
                    
                    _logger.Info("CatchupMode is set to {0}", options.GeneralOptions.TaskConcurrency.CatchupMode);

                    _logger.Info("EnhanceChineseSearch is set to {0}", options.ModOptions.ChineseSearch.EnhanceChineseSearch);
                    var searchScope = string.Join(", ",
                        options.ModOptions.ChineseSearch.SearchScope
                            ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s =>
                                Enum.TryParse(s.Trim(), true, out ChineseSearchOptions.SearchItemType type)
                                    ? Utility.GetEnumDescription(type)
                                    : null)
                            .Where(d => d != null) ?? Enumerable.Empty<string>());
                    _logger.Info("EnhanceChineseSearch - SearchScope is set to {0}",
                        string.IsNullOrEmpty(searchScope) ? "ALL" : searchScope);
                    _logger.Info("ExcludeOriginalTitleFromSearch is set to {0}", options.ModOptions.ChineseSearch.ExcludeOriginalTitleFromSearch);

                }

                if (suppress) _currentSuppressOnOptionsSaved = false;
            }
        }
    }
}
