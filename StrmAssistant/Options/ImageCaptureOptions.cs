using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 提取 · 图像捕获分组：无主图视频的截帧、截帧位置与画面裁剪规则。
    /// 注意：<see cref="VisibleConditionAttribute"/> / <see cref="EnabledConditionAttribute"/> 的目标属性
    /// 按**同级路径**解析，本组所需的 helper 必须留在本类内。
    /// </summary>
    public class ImageCaptureOptions : EditableOptionsBase
    {
        [DisplayNameL("MediaInfoExtractOptions_Group_ImageCapture", typeof(Resources))]
        public override string EditorTitle => Resources.MediaInfoExtractOptions_Group_ImageCapture;

        [DisplayNameL("PluginOptions_EnableImageCapture_Enable_Image_Capture", typeof(Resources))]
        [DescriptionL("PluginOptions_EnableImageCapture_Perform_image_capture_for_videos_without_primary_image__Default_is_False_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool EnableImageCapture { get; set; } = false;

        [DisplayNameL("MediaInfoExtractOptions_ImageCaptureOffset_Image_Capture_Offset", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_ImageCaptureOffset_Image_capture_position_as_a_percentage_of_runtime__Default_is_10_", typeof(Resources))]
        [Required, MinValue(10), MaxValue(90)]
        [VisibleCondition(nameof(EnableImageCapture), SimpleCondition.IsTrue)]
        public int ImageCapturePosition { get; set; } = 10;

        [DisplayNameL("MediaInfoExtractOptions_ForcePortraitPoster_Force_Portrait_Poster", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_ForcePortraitPoster_Crop_the_captured_primary_image_to_2_3_portrait_after_refresh__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnableImageCapture), SimpleCondition.IsTrue)]
        public bool ForcePortraitPoster { get; set; } = false;

        [DisplayNameL("MediaInfoExtractOptions_EnsureEpisode16x9_Ensure_Episode_Image_16x9", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_EnsureEpisode16x9_Crop_episode_stills_to_16_9_when_the_source_ratio_differs__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnableImageCapture), SimpleCondition.IsTrue)]
        public bool EnsureEpisode16x9 { get; set; } = false;

        [Browsable(false)]
        public string ImageCaptureExcludeMediaContainers { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
