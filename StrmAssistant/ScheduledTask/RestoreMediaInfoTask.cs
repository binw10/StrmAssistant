using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Common;
using StrmAssistant.Mod;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    public class RestoreMediaInfoTask : IScheduledTask
    {
        private readonly ILogger _logger = Plugin.Instance.Logger;
        private readonly IFileSystem _fileSystem;

        public RestoreMediaInfoTask(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            if (!Plugin.IsPluginEnabled) return;

            _logger.Info("MediaInfoRestore - Scheduled Task Execute");

            var candidates = Plugin.LibraryApi.FetchPreExtractTaskItems()
                .Where(i => i is Video || i is Audio)
                .ToList();
            _logger.Info("MediaInfoRestore - Number of candidates: " + candidates.Count);

            var directoryService = new DirectoryService(_logger, _fileSystem);
            double total = candidates.Count;
            var index = 0;
            var current = 0;
            var restored = 0;
            var skippedExisting = 0;
            var skippedNoJson = 0;
            var failed = 0;
            var tasks = new List<Task>();

            try
            {
                foreach (var item in candidates)
                {
                    var semaphore = QueueManager.Tier2Semaphore;
                    try
                    {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        semaphore.Release();
                        _logger.Info("MediaInfoRestore - Scheduled Task Cancelled");
                        break;
                    }

                    var taskIndex = ++index;
                    var taskItem = item;
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger.Info("MediaInfoRestore - Scheduled Task Cancelled");
                                return;
                            }

                            if (Plugin.LibraryApi.HasMediaInfo(taskItem))
                            {
                                Interlocked.Increment(ref skippedExisting);
                                return;
                            }

                            var jsonPath = MediaInfoApi.GetMediaInfoJsonReadPath(taskItem, directoryService);
                            var jsonFile = directoryService.GetFile(jsonPath);
                            if (jsonFile?.Exists != true)
                            {
                                Interlocked.Increment(ref skippedNoJson);
                                return;
                            }

                            var refreshOptions = Plugin.MediaInfoApi.GetMediaInfoRefreshOptions();
                            var ds = refreshOptions.DirectoryService ?? directoryService;

                            ExclusiveExtract.AllowExtractInstance(taskItem);
                            if (taskItem is Video)
                            {
                                ChapterChangeTracker.BypassInstance(taskItem);
                            }

                            var result = await Plugin.MediaInfoApi
                                .DeserializeMediaInfo(taskItem, ds, "Restore MediaInfo Task", true, cancellationToken)
                                .ConfigureAwait(false);

                            if (!result)
                            {
                                Interlocked.Increment(ref failed);
                                _logger.Info($"MediaInfoRestore - Restore failed: {taskItem.Name} - {taskItem.Path}");
                                return;
                            }

                            Interlocked.Increment(ref restored);
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                if (Plugin.SubtitleApi.HasExternalSubtitleChanged(taskItem, ds, true))
                                {
                                    await Plugin.SubtitleApi
                                        .UpdateExternalSubtitles(taskItem, refreshOptions, false, true)
                                        .ConfigureAwait(false);
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.Warn($"MediaInfoRestore - Subtitle refresh failed: {taskItem.Name} - {e.Message}");
                            }

                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Info($"MediaInfoRestore - Item cancelled: {taskItem.Name} - {taskItem.Path}");
                        }
                        catch (Exception e)
                        {
                            Interlocked.Increment(ref failed);
                            _logger.Error($"MediaInfoRestore - Item failed: {taskItem.Name} - {taskItem.Path}");
                            _logger.Error(e.Message);
                            _logger.Debug(e.StackTrace);
                        }
                        finally
                        {
                            semaphore.Release();

                            var currentCount = Interlocked.Increment(ref current);
                            progress.Report(currentCount / total * 100);
                            _logger.Info($"MediaInfoRestore - Progress {currentCount}/{total} - Task {taskIndex}: " +
                                         $"{taskItem.Path}");
                        }
                    });
                    tasks.Add(task);
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }
            finally
            {
                // Cancellation stops scheduling, but completion must include every started worker.
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();

            progress.Report(100.0);
            _logger.Info($"MediaInfoRestore - Summary: restored={restored}, alreadyExists={skippedExisting}, " +
                         $"noJson={skippedNoJson}, failed={failed}");
            _logger.Info("MediaInfoRestore - Scheduled Task Complete");
        }

        public string Category => Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
            Plugin.Instance.DefaultUICulture);

        public string Key => "MediaInfoRestoreTask";

        public string Description => Resources.ResourceManager.GetString(
            "RestoreMediaInfoTask_Description_Restores_media_info_from_JSON_file", Plugin.Instance.DefaultUICulture);

        public string Name => Resources.ResourceManager.GetString("RestoreMediaInfoTask_Name_Restore_MediaInfo",
            Plugin.Instance.DefaultUICulture);

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
