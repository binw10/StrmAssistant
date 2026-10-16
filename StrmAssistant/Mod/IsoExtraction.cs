using HarmonyLib;
using MediaBrowser.Model.MediaInfo;
using StrmAssistant.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// ISO 提取（跨平台，免第三方强依赖）。
    ///
    /// 背景（Emby 4.10 源码实测）：
    /// - <c>Emby.Providers.MediaInfo.FFProbeProvider.IsSupported(path, protocol)</c> 对 <c>.iso</c>
    ///   后缀**硬编码返回 false**，导致 ISO 条目永远不会进入 ffprobe 探测。
    /// - <c>BaseMediaInfoProber.Probe</c> 中：内置 mounter（BlurayMounter/DvdMounter）的
    ///   <c>CanMount</c> 只认**目录**（BDMV/VIDEO_TS 文件夹）+ container=bluray/dvd，
    ///   对 <c>.iso</c> **文件**一律返回 false，因此 path 不会被替换为挂载路径，
    ///   随后 <c>IsSupported(".iso")</c> 返回 false，探测被整体跳过。
    ///
    /// 本模块的两步干预：
    /// 1. **解锁闸门**：Prefix <c>FFProbeProvider.IsSupported</c>，当选项开启且路径为
    ///    <c>.iso</c>（本地文件 / strm 解析出的 http(s) ISO URL）时返回 true。
    /// 2. **替换探测输入**：Prefix <c>MediaProbeManager.GetMediaInfoInternal</c>，把
    ///    <c>inputPath</c>（形如 <c>file:"D:\a.iso"</c> 或 <c>"http://x/a.iso"</c>）替换为
    ///    ffprobe 能稳定读取的本地路径：
    ///    - 本地 .iso → Windows 挂载取正片；失败退回进程内 UDF 选片（不改输入，交由 ffprobe 直读）。
    ///    - 远程 http(s) ISO → HTTP Range 拉取头部窗口到本地临时文件再探测。
    ///
    /// 关键安全性：
    /// - 只替换 `inputPath`（仅用于 ffprobe 参数），**不改** `MediaSourceInfo.Path`，
    ///   因此播放路径与条目身份完全不受影响。
    /// - 无法取得可用本地路径时**保守放行原路径**，不影响常规探测。
    /// - 选项 <c>EnableIsoExtraction</c> 默认 OFF。
    /// </summary>
    public class IsoExtraction : PatchBase<IsoExtraction>
    {
        private static MethodInfo _getMediaInfoInternal;
        private static MethodInfo _isSupported;

        public IsoExtraction()
        {
            Initialize();

            if (Plugin.Instance.MediaInfoExtractStore.GetOptions().EnableIsoExtraction)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var assembly = Assembly.Load("Emby.Server.MediaEncoding");
                var probeManager = assembly.GetType("Emby.Server.MediaEncoding.Probing.MediaProbeManager");

                _getMediaInfoInternal = probeManager?
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "GetMediaInfoInternal" && m.GetParameters().Length == 7);

                if (_getMediaInfoInternal == null)
                {
                    Plugin.Instance.Logger.Warn(
                        "IsoExtraction - MediaProbeManager.GetMediaInfoInternal not found; ISO extraction disabled");
                }

                // FFProbeProvider.IsSupported(string path, MediaProtocol protocol) —— 解锁 .iso 闸门
                var providers = Assembly.Load("Emby.Providers");
                var ffProbeProvider = providers.GetType("Emby.Providers.MediaInfo.FFProbeProvider");
                _isSupported = ffProbeProvider?
                    .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.Name == "IsSupported" && m.GetParameters().Length == 2);

                if (_isSupported == null)
                {
                    Plugin.Instance.Logger.Warn(
                        "IsoExtraction - FFProbeProvider.IsSupported not found; ISO extraction may be skipped by Emby");
                }
                else if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"IsoExtraction - resolved {_getMediaInfoInternal} / {_isSupported}, " +
                        $"mount supported={IsoMounter.IsSupported}, udf supported={IsoMounter.UdfReadSupported}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("IsoExtraction init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            if (_getMediaInfoInternal != null)
            {
                PatchUnpatch(PatchTracker, apply, _getMediaInfoInternal, prefix: nameof(GetMediaInfoInternalPrefix));
            }

            if (_isSupported != null)
            {
                PatchUnpatch(PatchTracker, apply, _isSupported, prefix: nameof(IsSupportedPrefix));
            }
        }

        /// <summary>
        /// 解锁 Emby 对 .iso 的探测闸门：仅当选项开启且目标为 .iso 时跳过原实现、直接返回 true。
        /// </summary>
        [HarmonyPrefix]
        private static bool IsSupportedPrefix(string path, MediaProtocol protocol, ref bool __result)
        {
            try
            {
                if (Plugin.Instance.MediaInfoExtractStore.GetOptions().EnableIsoExtraction &&
                    !string.IsNullOrWhiteSpace(path) &&
                    IsoMounter.LooksLikeIsoPath(path))
                {
                    __result = true;
                    return false;
                }
            }
            catch
            {
                // 出错时回退原实现
            }

            return true;
        }

        [HarmonyPrefix]
        private static void GetMediaInfoInternalPrefix(ref string inputPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inputPath)) return;

                var logger = Plugin.Instance.Logger;

                // ① 远程/流式 ISO（strm → http/https）：拉取头部窗口到本地临时文件
                if (IsoMounter.TryNormalizeRemoteIso(inputPath, out var isoUrl))
                {
                    var tempPath = IsoMounter.GetRemoteProbePath(isoUrl);
                    if (!string.IsNullOrEmpty(tempPath))
                    {
                        inputPath = tempPath;
                        logger.Info($"IsoExtraction - remote iso probing via downloaded window -> {tempPath}");
                    }
                    else
                    {
                        logger.Info($"IsoExtraction - remote iso window unavailable; leaving input unchanged: {isoUrl}");
                    }

                    return;
                }

                // ② 本地 .iso 文件：挂载取正片，失败退回进程内 UDF（不改探测输入）
                if (IsoMounter.TryNormalizeIsoPath(inputPath, out var isoPath))
                {
                    var probePath = IsoMounter.GetProbePath(isoPath);
                    if (!string.IsNullOrEmpty(probePath))
                    {
                        inputPath = probePath;
                        logger.Info($"IsoExtraction - probing mounted title for {isoPath} -> {probePath}");
                    }

                    return;
                }

                if (Plugin.Instance.DebugMode &&
                    inputPath.IndexOf(".iso", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Plugin.Instance.Logger.Debug($"IsoExtraction - iso input not handled: {inputPath}");
                }
            }
            catch
            {
                // 保持原路径，不影响常规探测
            }
        }
    }
}
