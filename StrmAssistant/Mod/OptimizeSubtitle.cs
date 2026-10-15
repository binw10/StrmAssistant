using HarmonyLib;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Net;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 为 Strm 优化 OpenSubtitles 搜索：Strm 条目的 MediaPath 非 File 协议时，
    /// OpenSubtitles 插件无法计算 moviehash、也无法按文件名搜索（搜索退化为
    /// 仅 IMDB/季集）。本补丁在 OpenSubtitleDownloader.Search 入口把 Strm 路径
    /// 解析为本地挂载路径后再交给原逻辑，hash 与文件名搜索自然恢复。
    /// 解析失败保持原路径，行为不变。
    /// </summary>
    public class OptimizeSubtitle : PatchBase<OptimizeSubtitle>
    {
        private static MethodInfo _search;

        public OptimizeSubtitle()
        {
            Initialize();

            if (Plugin.Instance.ExperienceEnhanceStore.GetOptions().OptimizeSubtitle)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var openSubtitlesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "OpenSubtitles");
                if (openSubtitlesAssembly == null)
                {
                    Plugin.Instance.Logger.Info("OptimizeSubtitle - OpenSubtitles plugin not loaded, skip");
                    return;
                }

                _search = openSubtitlesAssembly
                    .GetType("OpenSubtitles.OpenSubtitleDownloader")
                    ?.GetMethod("Search", new[] { typeof(SubtitleSearchRequest), typeof(CancellationToken) });

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"OptimizeSubtitle: Search={_search != null}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("OptimizeSubtitle init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _search, prefix: nameof(SearchPrefix));
        }

        [HarmonyPrefix]
        private static void SearchPrefix(SubtitleSearchRequest request)
        {
            if (request?.MediaPath is not { Length: > 0 } mediaPath) return;

            try
            {
                // 已是本地文件则无需处理
                if (Uri.TryCreate(mediaPath, UriKind.Absolute, out var uri) &&
                    uri.Scheme == Uri.UriSchemeFile) return;

                if (!string.Equals(Path.GetExtension(mediaPath), ".strm", StringComparison.OrdinalIgnoreCase) &&
                    uri?.Scheme != Uri.UriSchemeHttp && uri?.Scheme != Uri.UriSchemeHttps &&
                    uri?.Scheme != Uri.UriSchemeFtp) return;

                var localPath = Plugin.LibraryApi.GetStrmMountPath(mediaPath)
                    .GetAwaiter().GetResult();

                if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                {
                    Plugin.Instance.Logger.Debug(
                        $"OptimizeSubtitle - MediaPath resolved: {mediaPath} -> {localPath}");
                    request.MediaPath = localPath;
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("OptimizeSubtitle - resolve failed: " + ex.Message);
            }
        }
    }
}
