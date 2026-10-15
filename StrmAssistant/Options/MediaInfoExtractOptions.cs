using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using MediaBrowser.Model.MediaInfo;
using StrmAssistant.Common;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    public class MediaInfoExtractOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_EditorTitle_Strm_Extract", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_EditorTitle_Strm_Extract;

        [DisplayNameL("PluginOptions_IncludeExtra_Include_Extra", typeof(Resources))]
        [DescriptionL("PluginOptions_IncludeExtra_Include_media_extras_to_extract__Default_is_False_", typeof(Resources))]
        [Required]
        public bool IncludeExtra { get; set; } = false;

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
        [Required]
        public string ImageCaptureExcludeMediaContainers { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoExcludeKeywords_Media_Info_Exclude_Keywords", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoExcludeKeywords_Comma_separated_keywords_matched_against_the_file_path_to_skip_media_info_extraction__Default_is_EMPTY_", typeof(Resources))]
        [Required]
        public string MediaInfoExcludeKeywords { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoExcludeMediaContainers_Media_Info_Exclude_Media_Containers", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoExcludeMediaContainers_Comma_separated_container_names_or_extensions_to_skip_media_info_extraction__Default_is_EMPTY_", typeof(Resources))]
        [Required]
        public string MediaInfoExcludeMediaContainers { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_VideoThumbnailExcludeKeywords_Video_Thumbnail_Exclude_Keywords", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_VideoThumbnailExcludeKeywords_Comma_separated_keywords_matched_against_the_file_path_to_skip_video_thumbnail_extraction__Default_is_EMPTY_", typeof(Resources))]
        [Required]
        public string VideoThumbnailExcludeKeywords { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_VideoThumbnailExcludeMediaContainers_Video_Thumbnail_Exclude_Media_Containers", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_VideoThumbnailExcludeMediaContainers_Comma_separated_container_names_or_extensions_to_skip_video_thumbnail_extraction__Default_is_EMPTY_", typeof(Resources))]
        [Required]
        public string VideoThumbnailExcludeMediaContainers { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoExtractionTimeoutMs_Media_Info_Extraction_Timeout", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoExtractionTimeoutMs_Per_item_extraction_timeout_in_milliseconds__0_disables_the_timeout__Default_is_0_", typeof(Resources))]
        [Required, MinValue(0)]
        public int MediaInfoExtractionTimeoutMs { get; set; } = 0;

        [Browsable(false)]
        public bool HasExtractionTimeout => MediaInfoExtractionTimeoutMs > 0;

        [DisplayNameL("MediaInfoExtractOptions_ImageExtractionTimeoutMultiplier_Image_Extraction_Timeout_Multiplier", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_ImageExtractionTimeoutMultiplier_Multiplier_applied_to_the_extraction_timeout_when_image_capture_runs_for_the_item__Default_is_1_", typeof(Resources))]
        [Required, MinValue(1), MaxValue(10)]
        [VisibleCondition(nameof(HasExtractionTimeout), SimpleCondition.IsTrue)]
        public int ImageExtractionTimeoutMultiplier { get; set; } = 1;

        [DisplayNameL("ModOptions_ExclusiveExtract_Exclusive_Extract", typeof(Resources))]
        [DescriptionL("ModOptions_ExclusiveExtract_Only_allow_this_plugin_to_extract_media_info__ffprobe__and_capture_image__ffmpeg___Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool ExclusiveExtract { get; set; } = false;

        public enum ExclusiveControl
        {
            [DescriptionL("ExclusiveControl_IgnoreFileChange_IgnoreFileChange", typeof(Resources))]
            IgnoreFileChange,
            [DescriptionL("ExclusiveControl_ExtractOnFileChange_ExtractOnFileChange", typeof(Resources))]
            ExtractOnFileChange,
            [DescriptionL("ExclusiveControl_CatchAllAllow_CatchAllAllow", typeof(Resources))]
            CatchAllAllow,
            [DescriptionL("ExclusiveControl_CatchAllBlock_CatchAllBlock", typeof(Resources))]
            CatchAllBlock,
            [DescriptionL("ExclusiveControl_IgnoreExtSubChange_IgnoreExtSubChange", typeof(Resources))]
            IgnoreExtSubChange,
            [DescriptionL("ExclusiveControl_NoPersistIntegration_NoPersistIntegration", typeof(Resources))]
            NoPersistIntegration,
            [DescriptionL("ExclusiveControl_NoIntroProtect_NoIntroProtect", typeof(Resources))]
            NoIntroProtect,
            [DescriptionL("ExclusiveControl_NoNfoSaverOptimization_No_Nfo_Saver_Optimization", typeof(Resources))]
            NoNfoSaverOptimization,
            [DescriptionL("ExclusiveControl_ExtractAlternative_ExtractAlternative", typeof(Resources))]
            ExtractAlternative
        }

        [Browsable(false)]
        public List<EditorSelectOption> ExclusiveControlList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("MediaInfoExtractOptions_ExclusiveExtractFeatureControl_Exclusive_Extract_Feature_Control", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(ExclusiveControlList))]
        [VisibleCondition(nameof(ExclusiveExtract), SimpleCondition.IsTrue)]
        public string ExclusiveControlFeatures { get; set; } = string.Empty;

        public enum PersistMediaInfoOption
        {
            None,
            Default,
            Restore
        }

        [Browsable(false)]
        public List<EditorRadioOption> PersistMediaInfoOptionList { get; set; } = new List<EditorRadioOption>();

        [DisplayName("")]
        [SelectItemsSource(nameof(PersistMediaInfoOptionList))]
        [SelectShowRadioGroup]
        public string PersistMediaInfoMode { get; set; } = PersistMediaInfoOption.None.ToString();

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoJsonRootFolder_MediaInfo_Json_Root_Folder", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoJsonRootFolder_Store_or_load_media_info_JSON_files_under_this_root_folder__Default_is_EMPTY_", typeof(Resources))]
        [EditFolderPicker]
        [VisibleCondition(nameof(PersistMediaInfoMode), ValueCondition.IsNotEqual, PersistMediaInfoOption.None)]
        public string MediaInfoJsonRootFolder { get; set; } = string.Empty;

        [Browsable(false)]
        public List<EditorSelectOption> LibraryList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("PluginOptions_LibraryScope_Library_Scope", typeof(Resources))]
        [DescriptionL("PluginOptions_LibraryScope_Library_scope_to_extract__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string LibraryScope { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_CustomProbePath_Custom_Probe_Path", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_CustomProbePath_Custom_ffprobe_binary_used_for_media_info_extraction__leave_empty_to_use_the_Emby_builtin__supports_rffmpeg_wrapper_for_distributed_extraction_", typeof(Resources))]
        [EditFilePicker]
        [Required]
        public string CustomProbePath { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_CustomEncoderPath_Custom_Encoder_Path", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_CustomEncoderPath_Custom_ffmpeg_binary_used_for_image_capture_and_thumbnails__leave_empty_to_use_the_Emby_builtin__a_build_with_libplacebo_fixes_DV_P5_green_tint_", typeof(Resources))]
        [EditFilePicker]
        [Required]
        public string CustomEncoderPath { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsModSupported => RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public void Initialize(ILibraryManager libraryManager)
        {
            LibraryList.Clear();

            LibraryList.Add(new EditorSelectOption
            {
                Value = "-1",
                Name = Resources.Favorites,
                IsEnabled = true
            });

            var libraries = libraryManager.GetVirtualFolders();

            foreach (var item in libraries)
            {
                if (LibraryApi.ExcludedCollectionTypes.Contains(item.CollectionType))
                {
                    continue;
                }

                var selectOption = new EditorSelectOption
                {
                    Value = item.ItemId,
                    Name = item.Name,
                    IsEnabled = true,
                };

                LibraryList.Add(selectOption);
            }

            ExclusiveControlList.Clear();

            foreach (Enum item in Enum.GetValues(typeof(ExclusiveControl)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = Utility.GetEnumDescription(item),
                    IsEnabled = true,
                };

                ExclusiveControlList.Add(selectOption);
            }

            PersistMediaInfoOptionList.Clear();
            PersistMediaInfoOptionList = new List<EditorRadioOption>
            {
                new EditorRadioOption
                {
                    Value = PersistMediaInfoOption.Default,
                    PrimaryText = Resources.MediaInfoExtractOptions_PersistMediaInfo_Persist_MediaInfo,
                    SecondaryText =
                        Resources
                            .MediaInfoExtractOptions_PersistMediaInfo_Persist_media_info_in_JSON_file__Default_is_OFF_
                },
                new EditorRadioOption
                {
                    Value = PersistMediaInfoOption.Restore,
                    PrimaryText = Resources.MediaInfoExtractOptions_MediaInfoRestoreMode_MediaInfo_Restore_Mode,
                    SecondaryText =
                        Resources
                            .MediaInfoExtractOptions_MediaInfoRestoreMode_Only_restore_media_info__chapters__and_video_thumbnails_from_JSON_or_BIF__skipping_extraction__Default_is_OFF_
                },
                new EditorRadioOption
                {
                    Value = PersistMediaInfoOption.None,
                    PrimaryText = Resources.PersistMediaInfoOption_None_None
                }
            };
        }
    }
}
