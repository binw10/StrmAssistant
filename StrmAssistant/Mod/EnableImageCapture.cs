using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using StrmAssistant.Common;
using StrmAssistant.ScheduledTask;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class EnableImageCapture : PatchBase<EnableImageCapture>
    {
        private static ConstructorInfo _staticConstructor;
        private static FieldInfo _resourcePoolField;
        private static MethodInfo _isShortcutGetter;
        private static PropertyInfo _isShortcutProperty;
        private static MethodInfo _supportsVideoImageCapture;
        private static MethodInfo _supportsAudioEmbeddedImages;
        private static MethodInfo _getImage;
        private static MethodInfo _runExtraction;
        private static MethodInfo _getExtraInputArguments;
        private static Type _singleImageExtractor;
        private static MethodInfo _supportsThumbnailsGetter;
        private static MethodInfo _logThumbnailImageExtractionFailure;
        private static ConstructorInfo _baseOptionsConstructor;

        private static readonly AsyncLocal<BaseItem> ShortcutItem = new AsyncLocal<BaseItem>();
        private static readonly AsyncLocal<BaseItem> ImageCaptureItem = new AsyncLocal<BaseItem>();
        private static int _isShortcutPatchUsageCount;

        private static SemaphoreSlim SemaphoreFFmpeg;
        public static int SemaphoreFFmpegMaxCount { get; private set; }

        public EnableImageCapture()
        {
            SemaphoreFFmpegMaxCount = Plugin.Instance.MainOptionsStore.GetOptions().GeneralOptions.TaskConcurrency.MaxConcurrentCount;

            Initialize();

            if (Plugin.Instance.MediaInfoExtractStore.GetOptions().ImageCapture.EnableImageCapture)
            {
                SemaphoreFFmpeg = new SemaphoreSlim(SemaphoreFFmpegMaxCount);
                PatchResourcePool();
                var resourcePool = (SemaphoreSlim)_resourcePoolField?.GetValue(null);

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug("Current FFmpeg ResourcePool: " + resourcePool?.CurrentCount ??
                                                 string.Empty);
                }

                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var mediaEncodingAssembly = Assembly.Load("Emby.Server.MediaEncoding");
            var imageExtractorBaseType =
                mediaEncodingAssembly.GetType("Emby.Server.MediaEncoding.ImageExtraction.ImageExtractorBase");
            _staticConstructor = imageExtractorBaseType.GetConstructor(BindingFlags.Static | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            _resourcePoolField =
                imageExtractorBaseType.GetField("resourcePool", BindingFlags.NonPublic | BindingFlags.Static);
            _isShortcutGetter = typeof(BaseItem).GetProperty("IsShortcut", BindingFlags.Instance | BindingFlags.Public)
                ?.GetGetMethod();
            _isShortcutProperty =
                typeof(BaseItem).GetProperty("IsShortcut", BindingFlags.Instance | BindingFlags.Public);

            var embyProviders = Assembly.Load("Emby.Providers");
            var videoImageProvider = embyProviders.GetType("Emby.Providers.MediaInfo.VideoImageProvider");
            _supportsVideoImageCapture =
                videoImageProvider.GetMethod("Supports", BindingFlags.Instance | BindingFlags.Public);
            _getImage = videoImageProvider.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "GetImage")
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
            var audioImageProvider = embyProviders.GetType("Emby.Providers.MediaInfo.AudioImageProvider");
            _supportsAudioEmbeddedImages =
                audioImageProvider.GetMethod("Supports", BindingFlags.Instance | BindingFlags.Public);

            var supportsThumbnailsProperty =
                typeof(Video).GetProperty("SupportsThumbnails", BindingFlags.Public | BindingFlags.Instance);
            _supportsThumbnailsGetter = supportsThumbnailsProperty?.GetGetMethod();
            _runExtraction =
                imageExtractorBaseType.GetMethod("RunExtraction", BindingFlags.Instance | BindingFlags.Public);

            // IMG-6：抽帧输入参数注入点。GetExtraInputArguments 是命令列表中**唯一落在 -i 之前**、
            // 且允许派生类自定义的槽位（-ss/-f/探测参数之后，-i 之前）。
            // 基类返回 null；MultiImageExtractor 覆写为 "-copyts -start_at_zero"（覆写不经过基类，
            // 因此对基类打 Postfix 不会命中 MultiImageExtractor 的 BIF 路径）。
            _getExtraInputArguments = imageExtractorBaseType.GetMethod("GetExtraInputArguments",
                BindingFlags.Instance | BindingFlags.NonPublic);

            // IMG-2：4.10 起单帧抽取器由 QuickSingleImageExtractor 更名为 SingleImageExtractor。
            // 优先取新名、回退旧名（两者取第一个非 null），使「截图位置」覆盖在 4.9/4.10 上均能命中。
            const string imageExtractionNs = "Emby.Server.MediaEncoding.ImageExtraction.";
            _singleImageExtractor =
                mediaEncodingAssembly.GetType(imageExtractionNs + "SingleImageExtractor") ??
                mediaEncodingAssembly.GetType(imageExtractionNs + "QuickSingleImageExtractor");

            if (_singleImageExtractor == null)
            {
                Plugin.Instance.Logger.Warn(
                    "EnableImageCapture - SingleImageExtractor not found; image capture position override disabled");
            }
            else if (Plugin.Instance.DebugMode)
            {
                Plugin.Instance.Logger.Debug(
                    "EnableImageCapture - resolved single-image extractor type: " + _singleImageExtractor.FullName);
            }

            var embyServerImplementationsAssembly = Assembly.Load("Emby.Server.Implementations");
            var sqliteItemRepository =
                embyServerImplementationsAssembly.GetType("Emby.Server.Implementations.Data.SqliteItemRepository");
            _logThumbnailImageExtractionFailure = sqliteItemRepository.GetMethod("LogThumbnailImageExtractionFailure",
                BindingFlags.Public | BindingFlags.Instance);

            var optionDefCollection = AccessTools.TypeByName("Emby.Ffmpeg.Model.Options.Collections.OptionDefCollection");
            var optionOwner = AccessTools.TypeByName("Emby.Ffmpeg.Model.Options.Interfaces.IOptionOwner");
            _baseOptionsConstructor = AccessTools.Constructor(
                AccessTools.TypeByName("Emby.Ffmpeg.Model.Options.Collections.BaseOptions"),
                new[] { optionDefCollection, optionOwner });
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatchIsShortcut(apply);

            PatchUnpatch(PatchTracker, apply, _supportsVideoImageCapture, prefix: nameof(SupportsImageCapturePrefix),
                postfix: nameof(SupportsImageCapturePostfix));
            PatchUnpatch(PatchTracker, apply, _supportsAudioEmbeddedImages, prefix: nameof(SupportsImageCapturePrefix),
                postfix: nameof(SupportsImageCapturePostfix));
            PatchUnpatch(PatchTracker, apply, _getImage, prefix: nameof(GetImagePrefix));
            PatchUnpatch(PatchTracker, apply, _supportsThumbnailsGetter,
                prefix: nameof(SupportsThumbnailsGetterPrefix), postfix: nameof(SupportsThumbnailsGetterPostfix));
            PatchUnpatch(PatchTracker, apply, _runExtraction, prefix: nameof(RunExtractionPrefix));
            PatchUnpatch(PatchTracker, apply, _getExtraInputArguments, postfix: nameof(GetExtraInputArgumentsPostfix));
            PatchUnpatch(PatchTracker, apply, _logThumbnailImageExtractionFailure,
                prefix: nameof(LogThumbnailImageExtractionFailurePrefix));
            PatchUnpatch(PatchTracker, apply, _baseOptionsConstructor, prefix: nameof(BaseOptionsConstructorPrefix));
        }

        private static void PatchResourcePool()
        {
            var result = PatchUnpatch(Instance.PatchTracker, true, _staticConstructor,
                prefix: nameof(ResourcePoolPrefix));
            //var result = PatchUnpatch(Instance.PatchTracker, true, _staticConstructor,
            //    transpiler: nameof(ResourcePoolTranspiler));

            if (!result && Instance.PatchTracker.FallbackPatchApproach == PatchApproach.Reflection)
                PatchResourcePoolByReflection();
        }

        private static void PatchResourcePoolByReflection()
        {
            // 此方法需要修改后的 Emby.Server.MediaEncoding.dll，或者在某些版本中字段是可写的

            try
            {
                if (_resourcePoolField == null)
                {
                    Plugin.Instance.Logger.Warn("EnableImageCapture: ResourcePool field not found - Cannot control FFmpeg concurrency");
                    Plugin.Instance.Logger.Info("Image capture will still work but may not respect custom concurrency limits");
                    // 功能部分可用，不完全禁用
                    return;
                }

                // 检查字段是否为只读
                if (_resourcePoolField.IsInitOnly)
                {
                    Plugin.Instance.Logger.Info("EnableImageCapture: ResourcePool is readonly - Using alternative approach");
                    // 尝试其他方式或接受限制
                    Plugin.Instance.Logger.Info("Custom FFmpeg concurrency control not available on this Emby version");
                    Plugin.Instance.Logger.Info("Image capture feature will use default Emby concurrency settings");
                    return;
                }

                _resourcePoolField.SetValue(null, SemaphoreFFmpeg);

                Plugin.Instance.Logger.Info($"EnableImageCapture: FFmpeg ResourcePool patched successfully (max concurrent: {SemaphoreFFmpegMaxCount})");
                
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug("Patch FFmpeg ResourcePool Success by Reflection");
                }
            }
            catch (FieldAccessException fae)
            {
                Plugin.Instance.Logger.Warn("EnableImageCapture: Cannot modify ResourcePool field (access denied)");
                Plugin.Instance.Logger.Info("Image capture will use default Emby settings");
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"FieldAccessException: {fae.Message}");
                }
                // 不设置为None，功能仍然可用
            }
            catch (Exception re)
            {
                Plugin.Instance.Logger.Warn($"EnableImageCapture: Failed to patch ResourcePool via reflection: {re.Message}");
                Plugin.Instance.Logger.Info("Image capture will use default Emby concurrency settings");
                
                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug("Patch FFmpeg ResourcePool Failed by Reflection");
                    Plugin.Instance.Logger.Debug($"Exception type: {re.GetType().Name}");
                    Plugin.Instance.Logger.Debug(re.StackTrace);
                }

                // ResourcePool修改失败不应该完全禁用ImageCapture功能
                // 只是无法自定义并发控制，但基本功能仍然可用
            }
        }

        private void UnpatchResourcePool()
        {
            PatchUnpatch(PatchTracker, false, _staticConstructor, prefix: nameof(ResourcePoolPrefix));
            //PatchUnpatch(PatchTracker, false, _staticConstructor, transpiler: nameof(ResourcePoolTranspiler));

            var resourcePool = (SemaphoreSlim)_resourcePoolField.GetValue(null);
            Plugin.Instance.Logger.Info("Current FFmpeg Resource Pool: " + resourcePool?.CurrentCount ?? string.Empty);
        }

        public static void UpdateResourcePool(int maxConcurrentCount)
        {
            if (SemaphoreFFmpegMaxCount != maxConcurrentCount)
            {
                SemaphoreFFmpegMaxCount = maxConcurrentCount;
                SemaphoreSlim newSemaphoreFFmpeg;
                SemaphoreSlim oldSemaphoreFFmpeg;

                switch (Instance.PatchTracker.FallbackPatchApproach)
                {
                    case PatchApproach.Harmony:
                        Plugin.Instance.ApplicationHost.NotifyPendingRestart();

                        /* un-patch and re-patch don't work for readonly static field
                        UnpatchResourcePool();

                        _currentMaxConcurrentCount = maxConcurrentCount;
                        newSemaphoreFFmpeg = new SemaphoreSlim(maxConcurrentCount);
                        oldSemaphoreFFmpeg = SemaphoreFFmpeg;
                        SemaphoreFFmpeg = newSemaphoreFFmpeg;

                        PatchResourcePool();

                        oldSemaphoreFFmpeg.Dispose();
                        */
                        break;

                    case PatchApproach.Reflection:

                        newSemaphoreFFmpeg = new SemaphoreSlim(maxConcurrentCount);
                        oldSemaphoreFFmpeg = SemaphoreFFmpeg;
                        SemaphoreFFmpeg = newSemaphoreFFmpeg;

                        PatchResourcePoolByReflection();

                        oldSemaphoreFFmpeg.Dispose();

                        break;
                }
            }

            var resourcePool = (SemaphoreSlim)_resourcePoolField.GetValue(null);
            Plugin.Instance.Logger.Info("Current FFmpeg ResourcePool: " + resourcePool?.CurrentCount ?? string.Empty);
        }

        public static void PatchUnpatchIsShortcut(bool apply)
        {
            PatchUnpatch(Instance.PatchTracker, apply, _isShortcutGetter, ref _isShortcutPatchUsageCount,
                prefix: nameof(IsShortcutPrefix));
        }

        public static void AllowImageCaptureInstance(BaseItem item)
        {
            ImageCaptureItem.Value = item;
        }

        public static void PatchIsShortcutInstance(BaseItem item)
        {
            switch (Instance.PatchTracker.FallbackPatchApproach)
            {
                case PatchApproach.Harmony:
                    ShortcutItem.Value = item;
                    break;

                case PatchApproach.Reflection:
                    try
                    {
                        _isShortcutProperty.SetValue(item, true); //special logic depending on modded MediaBrowser.Controller.dll
                        //Plugin.Instance.Logger.Debug("Patch IsShortcut Success by Reflection" + " - " + item.Name + " - " + item.Path);
                    }
                    catch (Exception re)
                    {
                        if (Plugin.Instance.DebugMode)
                        {
                            Plugin.Instance.Logger.Debug("Patch IsShortcut Failed by Reflection");
                            Plugin.Instance.Logger.Debug(re.Message);
                        }

                        Instance.PatchTracker.FallbackPatchApproach = PatchApproach.None;
                    }
                    break;
            }
        }

        public static void UnpatchIsShortcutInstance(BaseItem item)
        {
            switch (Instance.PatchTracker.FallbackPatchApproach)
            {
                case PatchApproach.Harmony:
                    ShortcutItem.Value = null;
                    break;

                case PatchApproach.Reflection:
                    try
                    {
                        _isShortcutProperty.SetValue(item, false); //special logic depending on modded MediaBrowser.Controller.dll
                        //Plugin.Instance.Logger.Debug("Unpatch IsShortcut Success by Reflection" + " - " + item.Name + " - " + item.Path);
                    }
                    catch (Exception re)
                    {
                        if (Plugin.Instance.DebugMode)
                        {
                            Plugin.Instance.Logger.Debug("Unpatch IsShortcut Failed by Reflection");
                            Plugin.Instance.Logger.Debug(re.Message);
                        }
                    }
                    break;
            }
        }

        [HarmonyPrefix]
        private static bool ResourcePoolPrefix()
        {
            _resourcePoolField.SetValue(null, SemaphoreFFmpeg);
            return false;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ResourcePoolTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_1)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4_S,
                        (sbyte)Plugin.Instance.MainOptionsStore.GetOptions().GeneralOptions.TaskConcurrency.MaxConcurrentCount);
                    if (i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Ldc_I4_1)
                    {
                        codes[i + 1] = new CodeInstruction(OpCodes.Ldc_I4_S,
                            (sbyte)Plugin.Instance.MainOptionsStore.GetOptions().GeneralOptions.TaskConcurrency.MaxConcurrentCount);
                    }
                    break;
                }
            }
            return codes.AsEnumerable();
        }

        [HarmonyPrefix]
        private static bool IsShortcutPrefix(BaseItem __instance, ref bool __result)
        {
            if (ShortcutItem.Value != null && __instance.InternalId == ShortcutItem.Value.InternalId)
            {
                __result = false;
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        private static bool SupportsImageCapturePrefix(BaseItem item, ref bool __result, out bool __state)
        {
            __state = false;

            if (item.IsShortcut && ImageCaptureItem.Value != null &&
                ImageCaptureItem.Value.InternalId == item.InternalId)
            {
                PatchIsShortcutInstance(item);
                __state = true;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void SupportsImageCapturePostfix(BaseItem item, ref bool __result, bool __state)
        {
            if (__state)
            {
                UnpatchIsShortcutInstance(item);
            }
        }

        [HarmonyPrefix]
        private static bool GetImagePrefix(ref BaseMetadataResult itemResult)
        {
            if (itemResult != null && itemResult.MediaStreams != null)
            {
                itemResult.MediaStreams = itemResult.MediaStreams
                    .Where(ms => ms.Type != MediaStreamType.EmbeddedImage)
                    .ToArray();
            }

            return true;
        }

        private static long GetThumbnailPositionTicks(long runtimeTicks)
        {
            var percent = Plugin.Instance.MediaInfoExtractStore.GetOptions().ImageCapture.ImageCapturePosition / 100.0;

            var min = Math.Min(Convert.ToInt64(runtimeTicks * 0.5), TimeSpan.FromSeconds(20.0).Ticks);

            return Math.Max(Convert.ToInt64(runtimeTicks * percent), min);
        }

        [HarmonyPrefix]
        // ★ 4.9/4.10 兼容修复：只声明 prefix 实际使用的参数（__instance + ref inputPath + ref startOffset）
        //   4.9.5.0:  RunExtraction(string inputPath, IDictionary requestHeaders, MediaContainers? container, MediaStream videoStream,
        //              Nullable<MediaProtocol> protocol, int? streamIndex, Video3DFormat? threedFormat, TimeSpan? startOffset, ...)
        //   4.10.0.29: RunExtraction(MediaSourceInfo mediaSource, string inputPath, MediaContainers? container, MediaStream videoStream,
        //              MediaProtocol protocol (非 Nullable!), int? streamIndex, Video3DFormat? threedFormat, TimeSpan? startOffset, ...)
        //   两版本差异：4.9 首参=inputPath，4.10 首参=mediaSource 且 protocol 由 Nullable 改为非 Nullable。
        //   若 prefix 声明完整签名（尤其 MediaProtocol? protocol），4.10 会因参数类型不匹配生成非法 IL → CLR invalid program。
        //   解决：只声明 4.9/4.10 类型一致的 __instance / inputPath / startOffset，Harmony 按名匹配其余参数。
        private static void RunExtractionPrefix(object __instance, ref string inputPath, ref TimeSpan? startOffset)
        {
            var timeoutProperty = Traverse.Create(__instance).Property("TotalTimeoutMs");
            var origTimeout = timeoutProperty.GetValue<int>();
            var newTimeout = origTimeout *
                             Plugin.Instance.MainOptionsStore.GetOptions().GeneralOptions.TaskConcurrency.MaxConcurrentCount;

            if (ExtractVideoThumbnailTask.IsRunning)
            {
                timeoutProperty.SetValue(newTimeout);
            }

            // IMG-1：把 ISO 输入改写为 ffmpeg 可稳定读取的本地路径。
            // 抽帧的 -i 原先直接用 item.Path（ISO 路径 / strm 代理 URL）——远程直连会挂死、本地 .iso 无法定位；
            // 这里复用第 1 阶段的挂载/头部窗口基建改写 inputPath。非 ISO 输入原样放行。
            RewriteIsoInput(ref inputPath);

            if (ImageCaptureItem.Value != null && __instance.GetType() == _singleImageExtractor)
            {
                timeoutProperty.SetValue(newTimeout);

                if (startOffset.HasValue)
                {
                    var timeSpan =
                        ImageCaptureItem.Value.MediaContainer.GetValueOrDefault() == MediaContainers.Dvd ||
                        !ImageCaptureItem.Value.RunTimeTicks.HasValue || ImageCaptureItem.Value.RunTimeTicks.Value <= 0L
                            ? TimeSpan.FromSeconds(10.0)
                            : TimeSpan.FromTicks(GetThumbnailPositionTicks(ImageCaptureItem.Value.RunTimeTicks.Value));

                    startOffset = timeSpan;
                }

                ImageCaptureItem.Value = null;
            }
        }

        /// <summary>
        /// IMG-1：ISO 抽帧输入改写。
        ///
        /// - 本地 <c>.iso</c> → <see cref="IsoMounter.GetProbePath"/>（挂载正片，可 seek 到任意位置）；
        /// - 远程 http(s) ISO（strm 代理）→ <see cref="IsoMounter.GetRemoteProbePath"/>（HTTP Range 头部窗口临时文件）；
        /// - 取不到可用路径 → **保持原输入**（保守放行，绝不影响非 ISO 条目）。
        ///
        /// 只改写 ffmpeg 的 <c>inputPath</c> 参数，**不改** <c>MediaSourceInfo.Path</c>，因此条目身份与播放路径不受影响。
        /// </summary>
        private static void RewriteIsoInput(ref string inputPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(inputPath)) return;
                if (!IsoMounter.LooksLikeIsoPath(inputPath)) return;

                var logger = Plugin.Instance.Logger;

                // ① 远程/流式 ISO（strm → http/https）：下载头部窗口到本地临时文件再抽帧
                if (IsoMounter.TryNormalizeRemoteIso(inputPath, out var isoUrl))
                {
                    var tempPath = IsoMounter.GetRemoteProbePath(isoUrl);
                    if (!string.IsNullOrEmpty(tempPath))
                    {
                        logger.Info(
                            $"EnableImageCapture - iso input rewritten (remote window) {ShortenUrl(isoUrl)} -> {tempPath}");
                        inputPath = tempPath;
                    }
                    else
                    {
                        logger.Info(
                            $"EnableImageCapture - remote iso window unavailable; keeping original input: {ShortenUrl(isoUrl)}");
                    }

                    return;
                }

                // ② 本地 .iso：挂载取正片（可 seek）
                if (IsoMounter.TryNormalizeIsoPath(inputPath, out var isoPath))
                {
                    var probePath = IsoMounter.GetProbePath(isoPath);
                    if (!string.IsNullOrEmpty(probePath))
                    {
                        logger.Info($"EnableImageCapture - iso input rewritten (mounted title) {isoPath} -> {probePath}");
                        inputPath = probePath;
                    }
                    else
                    {
                        logger.Info($"EnableImageCapture - no mounted title for {isoPath}; keeping original input");
                    }
                }
            }
            catch (Exception e)
            {
                // 任何异常一律放行原输入，确保非 ISO 条目零副作用
                Plugin.Instance.Logger.Debug("EnableImageCapture - iso input rewrite skipped: " + e.Message);
            }
        }

        /// <summary>日志用：剥离 URL 查询串，避免把 account/pickcode 等写进日志。</summary>
        private static string ShortenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            var q = url.IndexOf('?');
            return q > 0 ? url.Substring(0, q) + "?..." : url;
        }

        /// <summary>
        /// IMG-6：为「由 ISO 改写而来」的海报抽帧追加 <c>-skip_frame nokey</c>，规避 mpegts 输入定位花屏。
        ///
        /// 根因（第 4 阶段遗留项 B 定论）：mpegts/m2ts 无索引，ffmpeg 的 **input seek**（<c>-ss</c> 在 <c>-i</c> 前）
        /// 只能按码率估算字节落点，落在非 IDR 附近时解码器需重同步，导致 seek 点后最初约 0.3~0.5s 的画面损坏
        /// （DV P7 FEL 盘尤甚，实测第 1~6 帧块状伪影，用户可见为「海报花屏」）。
        /// <c>-skip_frame nokey</c> 只解码关键帧：画面干净、落点误差 ≤1 GOP（本样本 1.0s），且不引入新失败面
        /// （普通 input seek 在片尾同样可能无帧）。
        ///
        /// **作用域严格限定**（双重闸门，确保非 ISO 条目零副作用）：
        /// 1. <see cref="IsoMounter.IsMountedIsoTitle"/> —— 仅命中本地 ISO 挂载出的正片
        ///    <c>…\BDMV\STREAM\*.m2ts</c>（远程 ISO 头部窗口**不**加，见该方法注释）；
        /// 2. 抽取器类型为单帧抽取器（海报）—— 只有海报路径带非零 <c>-ss</c>；
        ///    BIF/章节图路径（<c>ImageSeriesExtractor</c> 区间抽帧 <c>startOffset=null</c>、
        ///    <c>MultiImageExtractor</c> 走 <c>-skip_list</c>）**不需要也不应**加该参数。
        /// </summary>
        [HarmonyPostfix]
        private static void GetExtraInputArgumentsPostfix(object __instance, string inputPath, ref string __result)
        {
            try
            {
                if (__instance == null || __instance.GetType() != _singleImageExtractor) return;
                if (!IsoMounter.IsMountedIsoTitle(inputPath)) return;

                __result = string.IsNullOrEmpty(__result)
                    ? "-skip_frame nokey"
                    : __result + " -skip_frame nokey";

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"EnableImageCapture - skip_frame nokey added for mounted iso title poster input: {inputPath}");
                }
            }
            catch (Exception e)
            {
                // 任何异常一律放行原参数，确保非 ISO 条目零副作用
                Plugin.Instance.Logger.Debug("EnableImageCapture - skip_frame injection skipped: " + e.Message);
            }
        }

        [HarmonyPrefix]
        private static bool LogThumbnailImageExtractionFailurePrefix(long itemId, long dateModifiedUnixTimeSeconds)
        {
            return false;
        }

        [HarmonyPrefix]
        private static bool SupportsThumbnailsGetterPrefix(BaseItem __instance, ref bool __result,
            out (bool, ExtraType?) __state)
        {
            __state = new ValueTuple<bool, ExtraType?>(false, __instance.ExtraType);

            if (__instance.IsShortcut)
            {
                PatchIsShortcutInstance(__instance);
                __state.Item1 = true;
            }

            if (__instance.ExtraType.HasValue)
            {
                __instance.ExtraType = null;
            }

            return true;
        }

        [HarmonyPostfix]
        private static void SupportsThumbnailsGetterPostfix(BaseItem __instance, ref bool __result,
            (bool, ExtraType?) __state)
        {
            if (__state.Item1)
            {
                UnpatchIsShortcutInstance(__instance);
            }

            if (__result && __state.Item2.HasValue)
            {
                __instance.ExtraType = __state.Item2;

                if (__state.Item2 == ExtraType.Trailer || __state.Item2 == ExtraType.ThemeVideo)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPrefix]
        private static void BaseOptionsConstructorPrefix(ref List<object> commonOptions)
        {
            var seen = new HashSet<string>();

            for (var i = commonOptions.Count - 1; i >= 0; i--)
            {
                var option = commonOptions[i];
                var name = Traverse.Create(option).Property("Name").GetValue<string>();

                if (name == "threads" && !seen.Add(name))
                {
                    commonOptions.RemoveAt(i);
                }
            }
        }
    }
}
