using HarmonyLib;
using StrmAssistant.Common;
using System;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// ISO 提取（免第三方依赖版）：
    /// 挂钩 <c>Emby.Server.MediaEncoding.Probing.MediaProbeManager.GetMediaInfoInternal</c> 的
    /// 前缀，把「本地 .iso 文件」的探测输入替换为**挂载后**的正片路径
    /// （见 <see cref="IsoMounter"/>）。
    ///
    /// 关键安全性：
    /// - 只替换 `inputPath` / `primaryPath`（仅用于 ffprobe 参数），**不改** `MediaSourceInfo.Path`，
    ///   因此播放路径与条目身份完全不受影响。
    /// - 仅当路径是**本地存在的 .iso 文件**时才介入；其余情况（含 http 流、普通视频）原样放行。
    /// - 选项 <c>EnableIsoExtraction</c> 默认 OFF；Windows 以外或 <c>Mount-DiskImage</c> 不可用时自动跳过。
    /// </summary>
    public class IsoExtraction : PatchBase<IsoExtraction>
    {
        private static MethodInfo _getMediaInfoInternal;

        public IsoExtraction()
        {
            Initialize();

            if (Plugin.Instance.MediaInfoExtractStore.GetOptions().EnableIsoExtraction &&
                IsoMounter.IsSupported)
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
                else if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"IsoExtraction - resolved {_getMediaInfoInternal}, IsoMounter supported={IsoMounter.IsSupported}");
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
        }

        [HarmonyPrefix]
        private static void GetMediaInfoInternalPrefix(ref string inputPath)
        {
            try
            {
                if (!IsoMounter.TryNormalizeIsoPath(inputPath, out var isoPath))
                {
                    if (Plugin.Instance.DebugMode &&
                        inputPath?.IndexOf(".iso", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Plugin.Instance.Logger.Debug($"IsoExtraction - iso input not handled: {inputPath}");
                    }

                    return;
                }

                var probePath = IsoMounter.GetProbePath(isoPath);
                if (string.IsNullOrEmpty(probePath)) return;

                // 只替换 ffprobe 的输入串（保留 Emby 原有的 file:"..." 包裹形式）：
                // 绝不能改 primaryPath —— 它会被写进 MediaInfo.Path（即条目播放路径）。
                inputPath = inputPath.Replace(isoPath, probePath);

                Plugin.Instance.Logger.Info(
                    $"IsoExtraction - probing mounted title for {isoPath} -> {probePath}");
            }
            catch
            {
                // 保持原路径，不影响常规探测
            }
        }
    }
}
