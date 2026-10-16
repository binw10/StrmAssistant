using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 双层杜比 Vision Profile 7（BL + EL）检测。
    ///
    /// 判定逐条对齐 PRO 反编译实现（<c>_pro_tmp/decompiled_v2/A----------\A----------.cs:60063-60245</c>）：
    /// 纯「媒体流几何 + 色彩」启发式，**不依赖自定义 ffprobe，也不解析 DOVI side data**。
    ///
    /// 1. 视频流按像素面积降序取前二，必须至少两条；
    /// 2. 两条 codec 均为 <c>hevc</c>；
    /// 3. 面积大者（基础层 BL）的 <see cref="MediaStream.ExtendedVideoType"/> 必须为 <c>Hdr10</c>；
    /// 4. 宽比、高比均 ≈2.0（容差 0.1）；
    /// 5. 命中 → 面积小者即增强层（EL）。
    ///
    /// 注意：PRO 用 <c>Take(2)</c> 后判数量，因此实际门槛是「≥2 条视频流」而非「恰好 2 条」，
    /// 多出的流（如画中画/花絮轨）不参与判定。此处保持同样行为。
    /// </summary>
    public static class DvProfileDetector
    {
        private const double RatioTolerance = 0.1;
        private const double ExpectedRatio = 2.0;
        private const string Hevc = "hevc";

        /// <summary>
        /// 尝试识别双层 DV Profile 7。<paramref name="reason"/> 始终带出结论说明（用于日志），
        /// 无论命中与否。
        /// </summary>
        public static bool TryDetectDualLayerProfile7(IEnumerable<MediaStream> streams,
            out MediaStream enhancementLayer, out string reason)
        {
            enhancementLayer = null;

            var videos = streams?
                .Where(s => s != null && s.Type == MediaStreamType.Video &&
                            s.Width.HasValue && s.Height.HasValue && s.Width.Value > 0 && s.Height.Value > 0)
                .OrderByDescending(s => (long)s.Width.Value * s.Height.Value)
                .Take(2)
                .ToList();

            if (videos == null || videos.Count < 2)
            {
                reason = $"video stream count < 2 (found {videos?.Count ?? 0})";
                return false;
            }

            var bl = videos[0]; // 面积大者 = 基础层
            var el = videos[1]; // 面积小者 = 增强层

            if (!string.Equals(bl.Codec, Hevc, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(el.Codec, Hevc, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"codec is not hevc (bl={bl.Codec ?? "(null)"}, el={el.Codec ?? "(null)"})";
                return false;
            }

            if ((int)bl.ExtendedVideoType != (int)ExtendedVideoTypes.Hdr10)
            {
                reason = $"base layer extended video type is {bl.ExtendedVideoType}, not Hdr10";
                return false;
            }

            var widthRatio = bl.Width.Value / (double)el.Width.Value;
            if (Math.Abs(widthRatio - ExpectedRatio) > RatioTolerance)
            {
                reason = $"width ratio {widthRatio:F3} is not ~2.0";
                return false;
            }

            var heightRatio = bl.Height.Value / (double)el.Height.Value;
            if (Math.Abs(heightRatio - ExpectedRatio) > RatioTolerance)
            {
                reason = $"height ratio {heightRatio:F3} is not ~2.0";
                return false;
            }

            enhancementLayer = el;
            reason = $"base layer {bl.Width}x{bl.Height}, enhancement layer {el.Width}x{el.Height}";
            return true;
        }
    }
}
