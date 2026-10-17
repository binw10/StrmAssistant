using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Tasks;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Common;
using StrmAssistant.Core;
using StrmAssistant.Mod;
using StrmAssistant.Options.Store;
using StrmAssistant.Options.UIBaseClasses.Views;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace StrmAssistant.Options.View
{
    internal class HomePageView : PluginPageView
    {
        private readonly PluginInfo _pluginInfo;
        private readonly PluginOptionsStore _store;
        private readonly ITaskManager _taskManager;
        private readonly ISessionManager _sessionManager;

        public HomePageView(PluginInfo pluginInfo, PluginOptionsStore store,
            ITaskManager taskManager, ISessionManager sessionManager)
            : base(pluginInfo.Id)
        {
            _pluginInfo = pluginInfo;
            _store = store;
            _taskManager = taskManager;
            _sessionManager = sessionManager;
            ContentData = store.GetOptions();

            PluginOptions.Initialize();
            PluginOptions.GeneralOptions.Initialize();
            PluginOptions.ModOptions.Initialize();
            PluginOptions.AboutOptions.Initialize();

            PopulateRuntimeStatus(PluginOptions.GeneralOptions);
        }

        public PluginOptions PluginOptions => ContentData as PluginOptions;

        private void PopulateRuntimeStatus(GeneralOptions options)
        {
            options.RuntimeStatus.RuntimeStatusList.Clear();

            try
            {
                using var process = Process.GetCurrentProcess();
                var workingSetMb = process.WorkingSet64 / 1024 / 1024;
                var gcHeapMb = GC.GetTotalMemory(false) / 1024 / 1024;

                options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                {
                    PrimaryText = string.Format(Resources.RuntimeStatus_Memory, workingSetMb, gcHeapMb),
                    Icon = IconNames.memory
                });

                var runningTasks = _taskManager.ScheduledTasks
                    .Where(t => t.State == TaskState.Running)
                    .Select(t => t.ScheduledTask.Name)
                    .ToList();

                options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                {
                    PrimaryText = string.Format(Resources.RuntimeStatus_RunningTasks,
                        runningTasks.Count, runningTasks.Count > 0 ? string.Join(", ", runningTasks) : "-"),
                    Icon = IconNames.schedule
                });

                var sessions = _sessionManager.Sessions?
                    .Where(s => !string.IsNullOrEmpty(s.Client))
                    .ToList();
                var playingCount = sessions?.Count(s => s.FullNowPlayingItem != null) ?? 0;

                options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                {
                    PrimaryText = string.Format(Resources.RuntimeStatus_Sessions,
                        sessions?.Count ?? 0,
                        playingCount,
                        sessions is { Count: > 0 }
                            ? string.Join(", ", sessions.Take(3).Select(s => $"{s.Client}/{s.UserName}"))
                            : "-"),
                    Icon = IconNames.devices
                });

                options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                {
                    PrimaryText = string.Format(Resources.RuntimeStatus_SearchIndex,
                        EnhanceChineseSearch.GetSearchIndexStatus()),
                    Icon = IconNames.search
                });

                var processing = new List<string>();
                if (QueueManager.IsMediaInfoProcessTaskRunning)
                {
                    processing.Add(Resources.CatchupTask_MediaInfo_MediaInfo);
                }

                if (QueueManager.IsEpisodeRefreshProcessTaskRunning)
                {
                    processing.Add(Resources.CatchupTask_EpisodeRefresh_EpisodeRefresh);
                }

                options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                {
                    PrimaryText = string.Format(Resources.RuntimeStatus_ProcessingTasks,
                        processing.Count > 0 ? string.Join(", ", processing) : "-"),
                    Icon = IconNames.pending_actions
                });

                var mediaInfoQueue = QueueManager.MediaInfoExtractItemQueue.Count;
                var fingerprintQueue = QueueManager.FingerprintItemQueue.Count;
                var introQueue = QueueManager.IntroSkipItemQueue.Count;
                var episodeRefreshQueue = QueueManager.EpisodeRefreshItemQueue.Count;

                options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                {
                    PrimaryText = string.Format(Resources.RuntimeStatus_PendingQueue,
                        mediaInfoQueue + fingerprintQueue + introQueue + episodeRefreshQueue,
                        mediaInfoQueue, fingerprintQueue, introQueue, episodeRefreshQueue),
                    Icon = IconNames.queue
                });

                var conflicts = ConflictDetector.GetConflictingAssemblies();
                if (conflicts.Length > 0)
                {
                    options.RuntimeStatus.RuntimeStatusList.Add(new GenericListItem
                    {
                        PrimaryText = string.Format(Resources.RuntimeStatus_ConflictPlugin,
                            string.Join(", ", conflicts)),
                        Icon = IconNames.warning
                    });
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            switch (commandId)
            {
                case "DisclaimerDialog":
                    var disclaimerDialog = new DisclaimerDialogView(_pluginInfo);
                    return Task.FromResult<IPluginUIView>(disclaimerDialog);

                case "CleanMemoryNow":
                    MemoryCleaner.RequestCleanup("手动清理（选项页）");
                    PopulateRuntimeStatus(PluginOptions.GeneralOptions);
                    RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);

                case "RefreshStatus":
                    PopulateRuntimeStatus(PluginOptions.GeneralOptions);
                    RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);

                case "ScheduleIndexRebuild":
                    EnhanceChineseSearch.ScheduleIndexRebuild();
                    PopulateRuntimeStatus(PluginOptions.GeneralOptions);
                    RaiseUIViewInfoChanged();
                    return Task.FromResult<IPluginUIView>(this);

                case "OptimizeSearchIndex":
                {
                    var optimizeResult = EnhanceChineseSearch.OptimizeSearchIndex();
                    PopulateRuntimeStatus(PluginOptions.GeneralOptions);
                    RaiseUIViewInfoChanged();
                    Plugin.NotificationApi?.SendMessageToAdmins(
                        optimizeResult.HasValue
                            ? string.Format(Resources.GeneralOptions_OptimizeSearchIndex_Reclaimed,
                                optimizeResult.Value.ReclaimedPages, optimizeResult.Value.ReclaimedBytes)
                            : Resources.GeneralOptions_OptimizeSearchIndex_Failed,
                        8000);
                    return Task.FromResult<IPluginUIView>(this);
                }

                case "RestartServer":
                    Task.Run(async () =>
                    {
                        Plugin.NotificationApi?.SendMessageToAdmins(
                            $"[{Resources.PluginOptions_EditorTitle_Strm_Assistant}] " +
                            Resources.RuntimeStatus_ServerRestarting, 10000);
                        await Task.Delay(3000).ConfigureAwait(false);
                        Plugin.Instance.ApplicationHost.Restart();
                    }).FireAndForget(Plugin.Instance.Logger);
                    return Task.FromResult<IPluginUIView>(this);
            }

            return base.RunCommand(itemId, commandId, data);
        }

        public override Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            if (ContentData is PluginOptions options)
            {
                options.GeneralOptions.ValidateOrThrow();
            }

            _store.SetOptions(PluginOptions);
            return base.OnSaveCommand(itemId, commandId, data);
        }
    }
}
