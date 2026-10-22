using Emby.Media.Model.ProbeModel;
using HarmonyLib;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// HDR Vivid 标注 —— 对齐 参考实现的「探测 CUVA side_data → 落库 Comment → 覆写显示」三段链路。
    ///
    /// ① 探测：挂 <c>ProbeResultNormalizer.GetMediaInfo</c> 的 Postfix，读取 ffprobe 原始
    ///    <c>ProbeResult.streams</c> 中视频流的 <c>side_data_list</c>；命中 CUVA HDR Vivid 动态元数据
    ///    （side_data_type 含 "CUVA"）即把对应 <see cref="MediaStream.Comment"/> 写为 "HDR Vivid"。
    /// ② 覆写显示：挂 <see cref="MediaStream.GenerateVideoRange(bool)"/> 的 Postfix，当 isForDisplay
    ///    且 Comment 命中时把显示值改为 "HDR Vivid"，与 参考实现的 MediaStream 显示 Prefix 行为一致。
    ///
    /// 为什么必须自建探测：Emby 的 <c>VideoRange</c> 是计算属性，只依据 <c>ExtendedVideoType</c>
    /// （仅有 None/Hdr10/Hdr10Plus/HyperLogGamma/DolbyVision），不含 Vivid；CUVA side_data 属未知类型，
    /// Emby 不会写进任何可读字段。实测 library.db 的 MediaStreams2 表存在 Comment 列、且不存在
    /// SideDataJson 列，因此 <c>Comment</c> 是本仓唯一可持久化的落库载体（与参考实现选型一致）。
    ///
    /// 安全性：
    /// - 只在命中 CUVA 时改写 <c>MediaStream.Comment</c>（Emby 定义为可写），未命中一律不动。
    /// - 不改 <c>MediaSourceInfo.Path</c>，播放路径与条目身份不受影响。
    /// - 开关沿用 <c>EnableIsoExtraction</c>（与 <see cref="DolbyVisionProfile"/> 一致，默认 OFF）。
    /// </summary>
    public class HdrVividTagging : PatchBase<HdrVividTagging>
    {
        internal const string VividLabel = "HDR Vivid";

        private const string VideoCodecType = "video";
        private const string CuvaMarker = "CUVA";

        private static MethodInfo _getMediaInfo;
        private static MethodInfo _generateVideoRange;

        public HdrVividTagging()
        {
            Initialize();

            if (Plugin.Instance.MediaInfoExtractStore.GetOptions().IsoTool.EnableIsoExtraction)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var assembly = Assembly.Load("Emby.Server.MediaEncoding");
                var normalizer = assembly.GetType("Emby.Server.MediaEncoding.Probing.ProbeResultNormalizer");

                _getMediaInfo = normalizer?
                    .GetMethod("GetMediaInfo", BindingFlags.Instance | BindingFlags.Public);

                _generateVideoRange = typeof(MediaStream).GetMethod("GenerateVideoRange",
                    BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);

                if (_getMediaInfo == null || _generateVideoRange == null)
                {
                    Plugin.Instance.Logger.Warn(
                        $"HdrVividTagging - target method missing (GetMediaInfo={_getMediaInfo != null}, GenerateVideoRange={_generateVideoRange != null}); HDR Vivid tagging disabled");
                }
                else if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"HdrVividTagging - resolved [{_getMediaInfo}] and [{_generateVideoRange}]");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("HdrVividTagging init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            if (_getMediaInfo != null)
            {
                PatchUnpatch(PatchTracker, apply, _getMediaInfo, postfix: nameof(GetMediaInfoPostfix));
            }

            if (_generateVideoRange != null)
            {
                PatchUnpatch(PatchTracker, apply, _generateVideoRange, postfix: nameof(GenerateVideoRangePostfix));
            }
        }

        [HarmonyPostfix]
        private static void GetMediaInfoPostfix(ProbeResult data, bool isAudio, string path, MediaProtocol protocol,
            MediaInfo __result)
        {
            try
            {
                if (isAudio || data?.streams == null || __result?.MediaStreams == null) return;

                foreach (var probe in data.streams)
                {
                    if (!string.Equals(probe?.codec_type, VideoCodecType, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!HasVividSideData(probe.side_data_list)) continue;

                    var stream = FindVideoStream(__result.MediaStreams, probe.index);
                    if (stream == null) continue;
                    if (string.Equals(stream.Comment, VividLabel, StringComparison.Ordinal)) continue;

                    stream.Comment = VividLabel;
                    Plugin.Instance.Logger.Info($"HdrVividTagging - HDR Vivid tagged (probe index={probe.index})");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("HdrVividTagging GetMediaInfoPostfix failed: " + ex.Message);
            }
        }

        [HarmonyPostfix]
        private static void GenerateVideoRangePostfix(MediaStream __instance, bool isForDisplay, ref string __result)
        {
            try
            {
                if (!isForDisplay || __instance?.Type != MediaStreamType.Video) return;

                if (string.Equals(__instance.Comment, VividLabel, StringComparison.Ordinal))
                {
                    __result = VividLabel;
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("HdrVividTagging GenerateVideoRangePostfix failed: " + ex.Message);
            }
        }

        /// <summary>
        /// CUVA 005.1 即 HDR Vivid 的动态元数据；ffprobe 将其归为未知 side_data，
        /// side_data_type 形如 "HDR Dynamic Metadata CUVA 005.1 2021 (Vivid)"。
        /// 用 "CUVA" 子串匹配以兼容不同 ffprobe 版本的后缀差异。
        /// </summary>
        private static bool HasVividSideData(ProbeSideDataList sideData)
        {
            return sideData != null &&
                   sideData.Any(sd => sd?.side_data_type != null &&
                                      sd.side_data_type.IndexOf(CuvaMarker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// 按 ffprobe 流序号定位对应的视频 <see cref="MediaStream"/>；序号缺失时回退为首路视频流。
        /// </summary>
        private static MediaStream FindVideoStream(IEnumerable<MediaStream> streams, int? probeIndex)
        {
            var videos = streams.Where(s => s != null && s.Type == MediaStreamType.Video).ToList();
            if (videos.Count == 0) return null;

            if (probeIndex.HasValue)
            {
                var match = videos.FirstOrDefault(v => v.Index == probeIndex.Value);
                if (match != null) return match;
            }

            return videos[0];
        }
    }
}
