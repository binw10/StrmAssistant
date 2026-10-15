using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using StrmAssistant.Core;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Events;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Common;
using StrmAssistant.IntroSkip;
using StrmAssistant.Mod;
using StrmAssistant.Options;
using StrmAssistant.Options.Store;
using StrmAssistant.Options.View;
using StrmAssistant.Properties;
using StrmAssistant.ScheduledTask;
using StrmAssistant.Web.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static StrmAssistant.Options.ExperienceEnhanceOptions;
using static StrmAssistant.Options.GeneralOptions;
using static StrmAssistant.Options.MediaInfoExtractOptions;
using static StrmAssistant.Options.Utility;

namespace StrmAssistant
{
    public class Plugin : BasePlugin, IHasThumbImage, IHasUIPages
    {
        private List<IPluginUIPageController> _pages;
        public readonly PluginOptionsStore MainOptionsStore;
        public readonly MediaInfoExtractOptionsStore MediaInfoExtractStore;
        public readonly MetadataEnhanceOptionsStore MetadataEnhanceStore;
        public readonly IntroSkipOptionsStore IntroSkipStore;
        public readonly ExperienceEnhanceOptionsStore ExperienceEnhanceStore;

        public static Plugin Instance { get; private set; }
        public static LibraryApi LibraryApi { get; private set; }
        public static MediaInfoApi MediaInfoApi { get; private set; }
        public static ChapterApi ChapterApi { get; private set; }
        public static FingerprintApi FingerprintApi { get; private set; }
        public static NotificationApi NotificationApi { get; private set; }
        public static SubtitleApi SubtitleApi { get; private set; }
        public static PlaySessionMonitor PlaySessionMonitor { get; private set; }
        public static AutoMarkWatched AutoMarkWatched { get; private set; }
        public static IntroDbApi IntroDbApi { get; private set; }
        public static ImagePostProcessor ImagePostProcessor { get; private set; }
        public static global::StrmAssistant.Provider.Douban.DoubanAssist DoubanAssist { get; private set; }
        public static MetadataApi MetadataApi { get; private set; }
        public static VideoThumbnailApi VideoThumbnailApi { get; private set; }

        /// <summary>影音直链等功能使用的媒体库管理器与共享 HttpClient。</summary>
        public static ILibraryManager LibraryManager { get; private set; }

        public static IHttpClient HttpClient { get; private set; }

        /// <summary>共享 JSON 序列化器（本地 TMDB patches 合并等静态工具使用）。</summary>
        public static IJsonSerializer JsonSerializer { get; private set; }

        private readonly Guid _id = new Guid("63c322b7-a371-41a3-b11f-04f8418b37d8");

        public readonly ILogger Logger;
        public readonly IApplicationHost ApplicationHost;
        public readonly IApplicationPaths ApplicationPaths;

        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ITaskManager _taskManager;
        private readonly ISessionManager _sessionManager;

        public Plugin(IApplicationHost applicationHost, IApplicationPaths applicationPaths, ILogManager logManager,
            IFileSystem fileSystem, ILibraryManager libraryManager, ISessionManager sessionManager,
            IItemRepository itemRepository, INotificationManager notificationManager, ILibraryMonitor libraryMonitor,
            IMediaSourceManager mediaSourceManager, IMediaMountManager mediaMountManager,
            IProviderManager providerManager, IMediaProbeManager mediaProbeManager,
            ILocalizationManager localizationManager, IUserManager userManager, IUserDataManager userDataManager,
            IFfmpegManager ffmpegManager, IMediaEncoder mediaEncoder, IJsonSerializer jsonSerializer,
            IHttpClient httpClient, IServerApplicationHost serverApplicationHost,
            IServerConfigurationManager configurationManager, ITaskManager taskManager,
            IImageExtractionManager imageExtractionManager, IServerApplicationPaths serverApplicationPaths)
        {
            Instance = this;
            Logger = logManager.GetLogger(Name);
            Logger.Info("Plugin is getting loaded.");
            ApplicationHost = applicationHost;
            ApplicationPaths = applicationPaths;

            if (ApplicationHost.ApplicationVersion < new Version("4.9.1.0"))
            {
                throw new NotSupportedException(
                    $"StrmAssistant requires Emby >= 4.9.1.0, current: {ApplicationHost.ApplicationVersion}");
            }
            
            // 初始化核心架构组件
            try
            {
                // 初始化版本适配器
                EmbyVersionAdapter.Initialize(Logger, applicationHost.ApplicationVersion);
                
                // 初始化高性能反射系统
                FastReflection.Initialize(Logger);
                
                // 初始化性能监控
                PerformanceMonitor.Initialize(Logger);
                
                // 初始化服务定位器
                ServiceLocator.Initialize(Logger);
                
                Logger.Info($"Core architecture initialized - Emby {applicationHost.ApplicationVersion} (API Version: {EmbyVersionAdapter.Instance.ApiVersion})");
                Logger.Info("FastReflection and PerformanceMonitor enabled for optimal performance");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize core architecture: {ex.Message}");
                Logger.Error(ex.StackTrace);
            }

            _libraryManager = libraryManager;
            _userManager = userManager;
            _userDataManager = userDataManager;
            _providerManager = providerManager;
            _sessionManager = sessionManager;
            _fileSystem = fileSystem;
            _taskManager = taskManager;

            LibraryManager = libraryManager;
            HttpClient = httpClient;
            JsonSerializer = jsonSerializer;

            MainOptionsStore = new PluginOptionsStore(applicationHost, Logger, ConfigName);
            MediaInfoExtractStore =
                new MediaInfoExtractOptionsStore(applicationHost, Logger, ConfigName + "_" + nameof(MediaInfoExtractOptions));
            MetadataEnhanceStore =
                new MetadataEnhanceOptionsStore(applicationHost, Logger, ConfigName + "_" + nameof(MetadataEnhanceOptions));
            IntroSkipStore = new IntroSkipOptionsStore(applicationHost, Logger, ConfigName + "_" + nameof(IntroSkipOptions));
            ExperienceEnhanceStore = new ExperienceEnhanceOptionsStore(applicationHost, Logger,
                ConfigName + "_" + nameof(ExperienceEnhanceOptions));

            MigrateChineseSearchOptions();
            InitializeOptionCache();

            if (MainOptionsStore.GetOptions().AboutOptions.DebugMode)
            {
                DebugMode = true;
                MainOptionsStore.GetOptions().AboutOptions.DebugMode = false;
                MainOptionsStore.SavePluginOptionsSuppress();
            }
            else if (Debugger.IsAttached)
            {
                DebugMode = true;
            }

            if (IsModSupported)
            {
                Logger.Info("Initializing Harmony Mod patches...");

                try
                {
                    PatchManager.Initialize();

                    // 检查补丁状态并记录详细信息
                    var modSuccess = PatchManager.IsModSuccess();
                    if (!modSuccess)
                    {
                        Logger.Warn("Harmony Mod initialization completed with some failures. Check logs above for details.");
                    }
                    else
                    {
                        Logger.Info("Harmony Mod initialization completed successfully.");
                    }

                    // 在调试模式下输出详细诊断
                    if (DebugMode)
                    {
                        Logger.Debug(PatchManager.GetDiagnosticReport());
                    }
                }
                catch (Exception ex)
                {
                    // 兜底：补丁初始化异常不得导致插件加载失败、设置页无法打开
                    Logger.Error($"PatchManager initialization failed: {ex.Message}");
                    Logger.Error(ex.ToString());
                }
            }
            else
            {
                Logger.Info("Harmony Mod is not supported in this environment. Using reflection-based approach.");
            }
            
            // 执行启动健康检查
            try
            {
                HealthCheck.Initialize(Logger);
                var healthResult = HealthCheck.Instance.PerformHealthCheck();
                Logger.Info($"Startup Health Check: {healthResult.OverallStatus}");
                
                if (healthResult.OverallStatus != HealthStatus.Healthy)
                {
                    if (DebugMode)
                    {
                        Logger.Debug(healthResult.ToString());
                    }
                    else
                    {
                        Logger.Warn($"Health issues detected. Enable DebugMode for details.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Health check failed: {ex.Message}");
            }
            
            // 初始化优化建议系统
            try
            {
                OptimizationAdvisor.Initialize(Logger);
                var suggestions = OptimizationAdvisor.Instance.GetSuggestions();
                
                if (suggestions.Count > 0 && DebugMode)
                {
                    var criticalSuggestions = suggestions.Where(s => 
                        s.Priority == OptimizationPriority.Critical || s.Priority == OptimizationPriority.High).ToList();
                    
                    if (criticalSuggestions.Any())
                    {
                        Logger.Warn($"Found {criticalSuggestions.Count} optimization suggestions. Use OptimizationAdvisor for details.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Optimization advisor initialization failed: {ex.Message}");
            }
            
            // 启动定期性能报告（仅在DebugMode下，每60分钟一次）
            // 启动定期内存清理器（按配置启停，防止 Emby 内存持续增长）
            try
            {
                var memOpts = MainOptionsStore.GetOptions().GeneralOptions;
                MemoryCleaner.ApplySettings(Logger, memOpts.EnableMemoryCleanup, memOpts.MemoryCleanupIntervalMinutes);
            }
            catch (Exception ex)
            {
                Logger.Warn($"MemoryCleaner initialization failed: {ex.Message}");
            }

            if (DebugMode)
            {
                try
                {
                    PerformanceReporter.Initialize(Logger, intervalMinutes: 60);
                    Logger.Debug("PerformanceReporter started - Reports every 60 minutes");
                }
                catch (Exception ex)
                {
                    Logger.Debug($"PerformanceReporter initialization failed: {ex.Message}");
                }
            }

            LibraryApi = new LibraryApi(libraryManager, providerManager, fileSystem, mediaMountManager, userManager);
            MediaInfoApi = new MediaInfoApi(libraryManager, fileSystem, providerManager, mediaSourceManager,
                itemRepository, jsonSerializer, libraryMonitor);
            ChapterApi = new ChapterApi(libraryManager, itemRepository);
            IntroDbApi = new IntroDbApi(httpClient, jsonSerializer);
            ImagePostProcessor = new ImagePostProcessor(ffmpegManager);
            DoubanAssist = new global::StrmAssistant.Provider.Douban.DoubanAssist(httpClient, jsonSerializer,
                providerManager, fileSystem);
            FingerprintApi = new FingerprintApi(libraryManager, fileSystem, applicationPaths, ffmpegManager,
                mediaEncoder, mediaMountManager, jsonSerializer, itemRepository, serverApplicationHost);
            PlaySessionMonitor = new PlaySessionMonitor(libraryManager, userManager, sessionManager);
            AutoMarkWatched = new AutoMarkWatched(libraryManager, userManager, sessionManager, userDataManager);
            NotificationApi = new NotificationApi(notificationManager, userManager, sessionManager);
            SubtitleApi = new SubtitleApi(libraryManager, fileSystem, mediaProbeManager, localizationManager,
                itemRepository);
            MetadataApi = new MetadataApi(libraryManager, fileSystem, configurationManager, localizationManager,
                jsonSerializer, httpClient);
            VideoThumbnailApi = new VideoThumbnailApi(libraryManager, fileSystem, imageExtractionManager, itemRepository,
                mediaMountManager, serverApplicationPaths, libraryMonitor, ffmpegManager);
            ShortcutMenuHelper.Initialize(configurationManager);

            FfmpegPathOverride.Initialize(ffmpegManager, Logger);
            var mediaInfoOptions = MediaInfoExtractStore.GetOptions();
            FfmpegPathOverride.Apply(mediaInfoOptions.CustomProbePath, mediaInfoOptions.CustomEncoderPath);

            var metadataCacheOptions = MetadataEnhanceStore.GetOptions();
            if (metadataCacheOptions.IsMetadataCacheEnabled)
            {
                var (cacheOk, cacheError) =
                    LocalMetadataCache.ValidateAndInitialize(metadataCacheOptions.MetadataCacheRootFolder);
                Logger.Info(cacheOk
                    ? $"MetadataCache - initialized at {LocalMetadataCache.Root} (mode={metadataCacheOptions.MetadataCacheMode})"
                    : $"MetadataCache - root invalid ({cacheError}): {metadataCacheOptions.MetadataCacheRootFolder}");
            }

            if (MainOptionsStore.GetOptions().GeneralOptions.CatchupMode) QueueManager.Initialize();
            if (IntroSkipStore.GetOptions().EnableIntroSkip) PlaySessionMonitor.Initialize();
            if (ExperienceEnhanceStore.GetOptions().EnableAutoMarkWatched) AutoMarkWatched.Initialize();
            MemoryGuard.Apply(Logger,
                MainOptionsStore.GetOptions().GeneralOptions.EnableMemoryAutoRestart,
                MainOptionsStore.GetOptions().GeneralOptions.MemoryAutoRestartThresholdMB);

            MigrateImageCaptureExcludeContainers();

            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemUpdated += OnItemUpdated;
            _libraryManager.ItemRemoved += OnItemRemoved;
            _providerManager.RefreshCompleted += OnRefreshCompleted;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _userManager.UserCreated += OnUserCreated;
            _userManager.UserDeleted += OnUserDeleted;
            _userManager.UserConfigurationUpdated += OnUserConfigurationUpdated;
            _userDataManager.UserDataSaved += OnUserDataSaved;
            CollectionFolder.LibraryOptionsUpdated += OnLibraryOptionsUpdated;
        }

        /// <summary>
        /// 老配置迁移：新版 ffmpeg 对 ts/m2ts 截图已可靠，移出截图默认排除列表
        /// （仅当存量配置仍为旧默认值时重置，自定义值不动）。
        /// </summary>
        private void MigrateImageCaptureExcludeContainers()
        {
            try
            {
                const string legacyDefault = "MpegTs,Ts,M2Ts";
                var mediaInfoOptions = MediaInfoExtractStore.GetOptions();

                if (!string.Equals(mediaInfoOptions.ImageCaptureExcludeMediaContainers, legacyDefault,
                        StringComparison.Ordinal)) return;

                mediaInfoOptions.ImageCaptureExcludeMediaContainers = string.Empty;
                MediaInfoExtractStore.SetOptions(mediaInfoOptions);

                Logger.Info("MediaInfoExtract - Migrated ImageCaptureExcludeMediaContainers: ts/m2ts no longer excluded by default");
            }
            catch (Exception e)
            {
                Logger.Warn("MediaInfoExtract - Exclude containers migration failed: " + e.Message);
            }
        }

        private void OnRefreshCompleted(object sender, GenericEventArgs<RefreshProgressInfo> e)
        {
            if (_libraryManager.IsScanRunning) return;

            var options = ExperienceEnhanceStore.GetOptions();

            if (IntroSkipStore.GetOptions().EnableIntroDbScrape)
            {
                if (e.Argument.Item is Series refreshedSeries)
                {
                    _ = Task.Run(() => IntroDbApi.ScrapeSeriesAsync(refreshedSeries, CancellationToken.None));
                }
                else if (e.Argument.Item is Movie refreshedMovie)
                {
                    _ = Task.Run(() => IntroDbApi.ScrapeMovieAsync(refreshedMovie, CancellationToken.None));
                }
            }

            var mediaInfoOptions = MediaInfoExtractStore.GetOptions();

            if (mediaInfoOptions.EnableImageCapture &&
                (mediaInfoOptions.ForcePortraitPoster || mediaInfoOptions.EnsureEpisode16x9))
            {
                var refreshedItem = e.Argument.Item;

                _ = Task.Run(() => ImagePostProcessor.ProcessItem(refreshedItem,
                    mediaInfoOptions.ForcePortraitPoster, mediaInfoOptions.EnsureEpisode16x9));
            }

            if (MetadataEnhanceStore.GetOptions().EnableDoubanAssist)
            {
                if (e.Argument.Item is Movie doubanMovie)
                {
                    _ = Task.Run(() => DoubanAssist.ApplyAsync(doubanMovie, CancellationToken.None));
                }
                else if (e.Argument.Item is Series doubanSeries)
                {
                    _ = Task.Run(() => DoubanAssist.ApplyAsync(doubanSeries, CancellationToken.None));
                }
                else if (e.Argument.Item is Season doubanSeason)
                {
                    _ = Task.Run(() => DoubanAssist.ApplyAsync(doubanSeason, CancellationToken.None));
                }
            }

            if (options.MergeMultiVersion && e.Argument.Item.IsTopParent)
            {
                var library = e.Argument.CollectionFolders.OfType<CollectionFolder>().FirstOrDefault();

                if (library != null && (library.CollectionType == CollectionType.Movies.ToString() ||
                                        library.CollectionType == CollectionType.TvShows.ToString() &&
                                        options.MergeSeriesPreference == MergeSeriesScopeOption.GlobalScope ||
                                        library.CollectionType is null))
                {
                    MergeMultiVersionTask.CurrentScanLibrary.Value = library;

                    var mergeMoviesTask = _taskManager.ScheduledTasks.FirstOrDefault(t =>
                        t.ScheduledTask is MergeMultiVersionTask);

                    if (mergeMoviesTask != null)
                    {
                        _ = _taskManager.Execute(mergeMoviesTask, new TaskOptions());
                    }
                }
            }
        }

        private void OnUserCreated(object sender, GenericEventArgs<User> e)
        {
            LibraryApi.FetchUsers();
        }

        private void OnUserDeleted(object sender, GenericEventArgs<User> e)
        {
            LibraryApi.FetchUsers();
        }

        private void OnUserConfigurationUpdated(object sender, GenericEventArgs<User> e)
        {
            if (e.Argument.Policy.IsAdministrator) LibraryApi.FetchAdminOrderedViews();
        }

        private void OnLibraryOptionsUpdated(object sender, GenericEventArgs<Tuple<CollectionFolder, LibraryOptions>> e)
        {
            var library = e.Argument.Item1;

            if (!LibraryApi.ExcludedCollectionTypes.Contains(library.CollectionType))
            {
                LibraryApi.UpdateLibraryPathsInScope();

                if (library.CollectionType == CollectionType.TvShows.ToString() || library.CollectionType is null)
                {
                    PlaySessionMonitor.UpdateLibraryPathsInScope();
                    FingerprintApi.UpdateLibraryPathsInScope();

                    if (IntroSkipStore.GetOptions().UnlockIntroSkip)
                        FingerprintApi.UpdateLibraryIntroDetectionFingerprintLength();

                    if (ExperienceEnhanceStore.GetOptions().MergeMultiVersion)
                        LibraryApi.EnsureLibraryEnabledAutomaticSeriesGrouping();
                }
            }
        }

        private async void OnItemAdded(object sender, ItemChangeEventArgs e)
        {
            try
            {
                // 合集变更通知：条目加入合集
                if (ExperienceEnhanceStore.GetOptions().EnhanceNotificationSystem &&
                    (e.Parent ?? e.Item?.Parent) is BoxSet boxsetAdded)
                {
                    NotificationApi.BoxsetUpdateSendNotification(boxsetAdded, e.Item, true);
                }

                var deserializeResult = false;

                if (e.Item is Video && MediaInfoExtractStore.GetOptions().PersistMediaInfoMode !=
                    PersistMediaInfoOption.None.ToString())
                {
                    deserializeResult = LibraryApi.HasMediaInfo(e.Item);

                    var directoryService = new DirectoryService(Logger, _fileSystem);

                    if (!deserializeResult)
                    {
                        deserializeResult = await MediaInfoApi.DeserializeMediaInfo(e.Item, directoryService,
                            "OnItemAdded Restore", true).ConfigureAwait(false);
                    }
                    else
                    {
                        _ = MediaInfoApi.SerializeMediaInfo(e.Item.InternalId, directoryService, true,
                            "OnItemAdded Overwrite").ConfigureAwait(false);
                    }
                }

                if ((e.Item is Video || e.Item is Audio) && MainOptionsStore.PluginOptions.GeneralOptions.CatchupMode)
                {
                    if (IntroSkipStore.IntroSkipOptions.UnlockIntroSkip &&
                        IsCatchupTaskSelected(CatchupTask.Fingerprint) &&
                        e.Item is Episode && FingerprintApi.IsLibraryInScope(e.Item) &&
                        (!deserializeResult || FingerprintApi.IsExtractNeeded(e.Item)))
                    {
                        QueueManager.FingerprintItemQueue.Enqueue(e.Item);
                    }
                    else
                    {
                        if (IsCatchupTaskSelected(CatchupTask.MediaInfo) && !deserializeResult)
                        {
                            QueueManager.MediaInfoExtractItemQueue.Enqueue(e.Item);
                        }

                        if (IsCatchupTaskSelected(CatchupTask.IntroSkip) &&
                            e.Item is Episode && PlaySessionMonitor.IsLibraryInScope(e.Item))
                        {
                            if (!deserializeResult)
                            {
                                QueueManager.MediaInfoExtractItemQueue.Enqueue(e.Item);
                            }
                            else if (e.Item is Episode episode && ChapterApi.SeasonHasIntroCredits(episode))
                            {
                                QueueManager.IntroSkipItemQueue.Enqueue(episode);
                            }
                        }
                    }

                    if (IsCatchupTaskSelected(CatchupTask.EpisodeRefresh) && e.Item is Episode ep &&
                        LibraryApi.IsPremiereDateInScope(ep, DateTimeOffset.UtcNow.AddDays(-90), false))
                    {
                        QueueManager.EpisodeRefreshItemQueue.Enqueue(ep);
                    }
                }

                if (e.Item is Movie || e.Item is Series || e.Item is Episode)
                {
                    NotificationApi.FavoritesUpdateSendNotification(e.Item);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex.Message);
                Logger.Debug(ex.StackTrace);
            }
        }

        private void OnPlaybackStopped(object sender, PlaybackProgressEventArgs e)
        {
            if (e.Item is Video && MediaInfoExtractStore.MediaInfoExtractOptions.ExclusiveExtract &&
                IsExclusiveFeatureSelected(ExclusiveControl.ExtractAlternative))
            {
                var targetItem = MediaInfoApi.GetItemByMediaSourceId(e.Item, e.MediaSourceId);

                if (targetItem != null)
                {
                    var refreshOptions = MediaInfoApi.GetMediaInfoRefreshOptions();

                    MediaInfoApi.QueueRefreshAlternateVersions(targetItem, refreshOptions, false);
                }
            }
        }

        private void OnItemUpdated(object sender, ItemChangeEventArgs e)
        {
            if (MetadataEnhanceStore.GetOptions().EnhanceMovieDbPerson &&
                (e.UpdateReason & (ItemUpdateType.MetadataDownload | ItemUpdateType.MetadataImport)) != 0)
            {
                if (e.Item is Season season && season.IndexNumber > 0)
                {
                    MetadataApi.UpdateSeriesPeople(season.Parent as Series);
                }
                else if (e.Item is Series series)
                {
                    MetadataApi.UpdateSeriesPeople(series);
                }
            }

            // 增强通知系统：元数据/图像更新通知（发给管理员）
            if (ExperienceEnhanceStore.GetOptions().EnhanceNotificationSystem &&
                (e.Item is Video || e.Item is Audio))
            {
                if ((e.UpdateReason & ItemUpdateType.ImageUpdate) != 0)
                {
                    NotificationApi.ImageUpdateSendNotification(e.Item);
                }
                else if ((e.UpdateReason &
                          (ItemUpdateType.MetadataDownload | ItemUpdateType.MetadataEdit |
                           ItemUpdateType.MetadataImport)) != 0)
                {
                    var trackedFields = ExperienceEnhanceStore.GetOptions().MetadataUpdateTrackedFields;
                    if (MatchesTrackedFields(e, trackedFields))
                    {
                        NotificationApi.MetadataUpdateSendNotification(e.Item,
                            string.Format(Resources.Notification_MetadataUpdate_EventDescription.Replace("\\n",
                                Environment.NewLine), e.Item.Name, e.Item.Path,
                            DescribeUpdateReason(e.UpdateReason)));
                    }
                }
            }
        }

        /// <summary>
        /// MetadataUpdateTrackedFields 过滤：逗号分隔的字段名（如 Overview,CommunityRating），
        /// 为空表示全部通知。若 Emby 提供 ItemChangeEventArgs.MetadataChangedFields 则按其过滤，
        /// 否则退化为 UpdateReason 粒度（始终命中）。
        /// </summary>
        private static bool MatchesTrackedFields(ItemChangeEventArgs e, string trackedFields)
        {
            var tracked = (trackedFields ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => f.Length > 0)
                .ToArray();

            if (tracked.Length == 0) return true;

            try
            {
                var property = typeof(ItemChangeEventArgs).GetProperty("MetadataChangedFields",
                    BindingFlags.Public | BindingFlags.Instance);
                if (property?.GetValue(e) is IEnumerable<string> changed)
                {
                    var changedFields = changed as string[] ?? changed.ToArray();
                    if (changedFields.Length == 0) return true;

                    return changedFields.Any(c => tracked.Any(t =>
                        string.Equals(t, c, StringComparison.OrdinalIgnoreCase)));
                }
            }
            catch (Exception)
            {
                // 字段不可用则不阻断通知
            }

            return true;
        }

        private static string DescribeUpdateReason(ItemUpdateType reason)
        {
            if ((reason & ItemUpdateType.MetadataDownload) != 0) return "MetadataDownload";
            if ((reason & ItemUpdateType.MetadataEdit) != 0) return "MetadataEdit";
            if ((reason & ItemUpdateType.MetadataImport) != 0) return "MetadataImport";
            if ((reason & ItemUpdateType.ImageUpdate) != 0) return "ImageUpdate";
            return reason.ToString();
        }

        private void OnItemRemoved(object sender, ItemChangeEventArgs e)
        {
            // 合集变更通知：条目移出合集
            if (ExperienceEnhanceStore.GetOptions().EnhanceNotificationSystem &&
                (e.Parent ?? e.Item?.Parent) is BoxSet boxsetRemoved)
            {
                NotificationApi.BoxsetUpdateSendNotification(boxsetRemoved, e.Item, false);
            }

            if (e.Item is Video && MediaInfoExtractStore.GetOptions().PersistMediaInfoMode !=
                PersistMediaInfoOption.Restore.ToString())
            {
                var directoryService = new DirectoryService(Logger, _fileSystem);
                MediaInfoApi.DeleteMediaInfoJson(e.Item, directoryService, "Item Removed Event");
            }

            if (e.Item is CollectionFolder library)
            {
                if (library.CollectionType == CollectionType.BoxSets.ToString() &&
                    !ExperienceEnhanceStore.GetOptions().UIFunctionOptions.NoBoxsetsAutoCreation)
                {
                    PatchManager.NoBoxsetsAutoCreation.Patch();
                    ExperienceEnhanceStore.GetOptions().UIFunctionOptions.NoBoxsetsAutoCreation = true;
                    ExperienceEnhanceStore.SavePluginOptionsSuppress();
                }

                if (!LibraryApi.ExcludedCollectionTypes.Contains(library.CollectionType))
                {
                    LibraryApi.UpdateLibraryPathsInScope();

                    if (library.CollectionType == CollectionType.TvShows.ToString() || library.CollectionType is null)
                    {
                        PlaySessionMonitor.UpdateLibraryPathsInScope();
                        FingerprintApi.UpdateLibraryPathsInScope();
                    }
                }
            }
        }

        private void OnUserDataSaved(object sender, UserDataSaveEventArgs e)
        {
            if (e.UserData.IsFavorite)
            {
                if (MainOptionsStore.PluginOptions.GeneralOptions.CatchupMode &&
                    IntroSkipStore.IntroSkipOptions.UnlockIntroSkip && IsCatchupTaskSelected(CatchupTask.Fingerprint) &&
                    FingerprintApi.IsLibraryInScope(e.Item) && (e.Item is Series || e.Item is Episode))
                {
                    QueueManager.FingerprintItemQueue.Enqueue(e.Item);
                }
                else if (MainOptionsStore.PluginOptions.GeneralOptions.CatchupMode &&
                         IsCatchupTaskSelected(CatchupTask.MediaInfo))
                {
                    QueueManager.MediaInfoExtractItemQueue.Enqueue(e.Item);
                }
            }
        }

        public override void OnUninstalling()
        {
            if (MainOptionsStore.GetOptions().ModOptions.EnhanceChineseSearch)
            {
                _ = NotificationApi.SendMessageToAdmins(
                    $"[{Resources.PluginOptions_EditorTitle_Strm_Assistant}] {Resources.Uninstall_Warning}", 10000);
            }

            MemoryCleaner.DisposeInstance();

            base.OnUninstalling();
        }

        private void MigrateChineseSearchOptions()
        {
            try
            {
                var configDir = ApplicationPaths.PluginConfigurationsPath;
                Directory.CreateDirectory(configDir);

                var legacyMarkerPath = Path.Combine(configDir, $"{Name}_chinese_search_migrated_2_2_0.flag");
                var versionMarkerPath = Path.Combine(configDir, $"{Name}_chinese_search_last_seen_version.txt");

                var currentVersionText = CurrentVersion ?? "0.0.0.0";
                if (!Version.TryParse(currentVersionText, out var currentVersion))
                {
                    currentVersion = new Version(0, 0, 0, 0);
                }

                var hasLegacyMarker = File.Exists(legacyMarkerPath);
                var hasVersionMarker = File.Exists(versionMarkerPath);
                var previousVersionText = hasVersionMarker ? File.ReadAllText(versionMarkerPath).Trim() : string.Empty;

                Version previousVersion = null;
                if (!string.IsNullOrWhiteSpace(previousVersionText) && Version.TryParse(previousVersionText, out var parsedPrevious))
                {
                    previousVersion = parsedPrevious;
                }

                Logger.Info(
                    $"EnhanceChineseSearch upgrade-check: current={currentVersion}, previous={(previousVersion?.ToString() ?? "<none>")}, legacyMarker={hasLegacyMarker}, versionMarker={hasVersionMarker}");

                // 首次安装/首次引入版本比较策略：仅记录版本，不执行自动禁用。
                if (previousVersion is null)
                {
                    File.WriteAllText(versionMarkerPath, currentVersion.ToString());

                    if (!hasLegacyMarker)
                    {
                        File.WriteAllText(legacyMarkerPath, DateTime.UtcNow.ToString("O"));
                    }

                    Logger.Info("EnhanceChineseSearch upgrade-check: baseline initialized, no auto-disable on first run.");
                    return;
                }

                var isUpgrade = currentVersion > previousVersion;
                if (!isUpgrade)
                {
                    if (currentVersion < previousVersion)
                    {
                        Logger.Warn($"EnhanceChineseSearch upgrade-check: detected downgrade {previousVersion} -> {currentVersion}; keep current option state.");
                    }

                    File.WriteAllText(versionMarkerPath, currentVersion.ToString());
                    return;
                }

                var options = MainOptionsStore.GetOptions();
                var changed = false;

                if (options.ModOptions.EnhanceChineseSearch)
                {
                    options.ModOptions.EnhanceChineseSearch = false;
                    changed = true;
                }

                if (options.ModOptions.EnhanceChineseSearchRestore)
                {
                    options.ModOptions.EnhanceChineseSearchRestore = false;
                    changed = true;
                }

                if (changed)
                {
                    MainOptionsStore.SavePluginOptionsSuppress();
                    Logger.Info(
                        $"EnhanceChineseSearch has been disabled after upgrade ({previousVersion} -> {currentVersion}). User must re-enable it manually to trigger rebuild.");
                }
                else
                {
                    Logger.Info($"EnhanceChineseSearch upgrade-check: no disable needed for upgrade {previousVersion} -> {currentVersion}.");
                }

                File.WriteAllText(versionMarkerPath, currentVersion.ToString());
                if (!hasLegacyMarker)
                {
                    File.WriteAllText(legacyMarkerPath, DateTime.UtcNow.ToString("O"));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to migrate Chinese search options: {ex.Message}");
                if (DebugMode)
                {
                    Logger.Debug(ex.StackTrace);
                }
            }
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public override string Description => "Extract MediaInfo and Enable IntroSkip";

        public override Guid Id => _id;

        public sealed override string Name => "StrmAssistantPlus";

        /// <summary>
        /// 配置文件命名前缀（保持 StrmAssistant 不变以延续旧版配置）。
        /// </summary>
        public string ConfigName => "StrmAssistant";

        public string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        public string UserAgent => $"{Name}/{CurrentVersion}";

        public CultureInfo DefaultUICulture =>
            new CultureInfo(MainOptionsStore.GetOptions().AboutOptions.DefaultUICulture);

        public bool DebugMode;

        public bool IsModSupported => RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public bool IsArm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public bool SupportsHarmony => RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Properties.thumb.png");
        }

        public IReadOnlyCollection<IPluginUIPageController> UIPageControllers
        {
            get
            {
                if (_pages == null)
                {
                    _pages = new List<IPluginUIPageController>
                    {
                        new MainPageController(GetPluginInfo(), _libraryManager, MainOptionsStore,
                            MediaInfoExtractStore, MetadataEnhanceStore, IntroSkipStore, ExperienceEnhanceStore,
                            _taskManager, _sessionManager)
                    };
                }

                return _pages.AsReadOnly();
            }
        }
    }
}
