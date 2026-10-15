using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.ScheduledTask
{
    /// <summary>
    /// 构建豆瓣缓存：遍历电影/剧集条目，预取豆瓣数据写入本地磁盘缓存
    /// （strmassistant_douban_cache.json + 评分快照），供后续刮削离线命中、
    /// 降低限流风险。已缓存的条目自动跳过，不产生网络请求。
    /// </summary>
    public class BuildDoubanCacheTask : IScheduledTask
    {
        private readonly ILogger _logger = Plugin.Instance.Logger;
        private readonly ILibraryManager _libraryManager;

        public BuildDoubanCacheTask(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("DoubanCacheBuild - Scheduled Task Execute");

            var options = Plugin.Instance.MetadataEnhanceStore.GetOptions();
            if (!options.EnableDoubanAssist)
            {
                _logger.Warn("DoubanCacheBuild - EnableDoubanAssist is off, task aborted");
                progress.Report(100.0);
                return;
            }

            var items = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[] { nameof(Movie), nameof(Series) }
            }).ToList();

            _logger.Info($"DoubanCacheBuild - Number of items: {items.Count}");

            var index = 0;
            var cached = 0;
            var skipped = 0;

            foreach (var item in items)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await Plugin.DoubanAssist.BuildCacheAsync(item, cancellationToken).ConfigureAwait(false))
                    {
                        cached++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("DoubanCacheBuild - Scheduled Task Cancelled");
                    return;
                }
                catch (Exception e)
                {
                    _logger.Error($"DoubanCacheBuild - Item failed: {item.Name}");
                    _logger.Error(e.Message);
                }
                finally
                {
                    index++;
                    progress.Report(index * 100.0 / items.Count);
                }
            }

            progress.Report(100.0);
            _logger.Info($"DoubanCacheBuild - Cached: {cached}, Skipped: {skipped}");
            _logger.Info("DoubanCacheBuild - Scheduled Task Complete");
        }

        public string Category =>
            Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
                Plugin.Instance.DefaultUICulture);

        public string Key => "BuildDoubanCacheTask";

        public string Description => Resources.ResourceManager.GetString(
            "BuildDoubanCacheTask_Description_Pre_fetches_Douban_subject_data_for_movies_and_series_into_the_local_cache__Cached_items_are_skipped",
            Plugin.Instance.DefaultUICulture);

        public string Name => Resources.ResourceManager.GetString(
            "BuildDoubanCacheTask_Name_Build_Douban_Cache",
            Plugin.Instance.DefaultUICulture);

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
        }
    }
}
