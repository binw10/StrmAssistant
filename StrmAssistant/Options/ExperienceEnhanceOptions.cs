using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    public class ExperienceEnhanceOptions : EditableOptionsBase
    {
        [DisplayNameL("ExperienceEnhanceOptions_EditorTitle_Experience_Enhance", typeof(Resources))]
        public override string EditorTitle => Resources.ExperienceEnhanceOptions_EditorTitle_Experience_Enhance;
        
        [DisplayNameL("GeneralOptions_MergeMultiVersion_Merge_Multiple_Versions", typeof(Resources))]
        [DescriptionL("GeneralOptions_MergeMultiVersion_Auto_merge_multiple_versions_if_in_the_same_folder_", typeof(Resources))]
        [Required]
        public bool MergeMultiVersion { get; set; } = false;

        public enum MergeMoviesScopeOption
        {
            [DescriptionL("MergeScopeOption_FolderScope_FolderScope", typeof(Resources))]
            FolderScope,
            [DescriptionL("MergeScopeOption_LibraryScope_LibraryScope", typeof(Resources))]
            LibraryScope,
            [DescriptionL("MergeScopeOption_GlobalScope_GlobalScope", typeof(Resources))]
            GlobalScope
        }
        
        [DisplayNameL("ExperienceEnhanceOptions_MergeMoviePreferences_Movie_Merge_Preference", typeof(Resources))]
        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        public MergeMoviesScopeOption MergeMoviesPreference { get; set; } = MergeMoviesScopeOption.FolderScope;
        
        public enum MergeSeriesScopeOption
        {
            [DescriptionL("MergeScopeOption_LibraryScope_LibraryScope", typeof(Resources))]
            LibraryScope,
            [DescriptionL("MergeScopeOption_GlobalScope_GlobalScope", typeof(Resources))]
            GlobalScope
        }

        [DisplayNameL("ExperienceEnhanceOptions_MergeSeriesPreferences_Series_Merge_Preference", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_MergeSeriesPreferences_Series_merge_works_across_libraries_only_in_GlobalScope", typeof(Resources))]
        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public MergeSeriesScopeOption MergeSeriesPreference { get; set; } = MergeSeriesScopeOption.LibraryScope;

        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        public ButtonItem SplitMoviesButton =>
            new ButtonItem(
                Resources.ExperienceEnhanceOptions_SplitMovieButton_Split_multi_version_movies_in_all_libraries)
            {
                Icon = IconNames.clear_all, Data1 = "SplitMovies", ConfirmationPrompt = Resources.AreYouSureToContinue
            };

        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        public GenericItemList SplitMoviesProgress { get; set; } = new GenericItemList();

        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        public SpacerItem SplitMovieProgressSeparator { get; set; } = new SpacerItem(SpacerSize.Small);

        [DisplayNameL("ExperienceEnhanceOptions_FolderVideoGroupingLimit_Folder_Video_Grouping_Limit", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_FolderVideoGroupingLimit_Maximum_number_of_videos_in_the_same_folder_eligible_for_multi_version_merge__8_is_the_Emby_default__Default_is_8_", typeof(Resources))]
        [VisibleCondition(nameof(MergeMultiVersion), SimpleCondition.IsTrue)]
        [Required]
        public int FolderVideoGroupingLimit { get; set; } = 8;

        [DisplayNameL("ExperienceEnhanceOptions_EnhanceNotification_Enhance_Notification", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnhanceNotification_Show_episode_details_in_series_notification__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool EnhanceNotificationSystem { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_MetadataUpdateTrackedFields_Metadata_Update_Tracked_Fields", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_MetadataUpdateTrackedFields_Comma_separated_metadata_fields_that_trigger_the_metadata_update_notification__blank_notifies_all__Default_is_EMPTY_", typeof(Resources))]
        [VisibleCondition(nameof(EnhanceNotificationSystem), SimpleCondition.IsTrue)]
        public string MetadataUpdateTrackedFields { get; set; } = string.Empty;
        
        [DisplayNameL("ExperienceEnhanceOptions_EnableDeepDelete_Enable_Deep_Delete", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnableDeepDelete_Attempt_to_cascade_delete_the_underlying_file_of_strm_or_symlink__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        [Required]
        public bool EnableDeepDelete { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_EnableAutoMarkWatched_Auto_Mark_Previous_Episodes_Watched", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnableAutoMarkWatched_Automatically_mark_previous_episodes_as_watched_when_current_episode_reaches_the_progress_threshold__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableAutoMarkWatched { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_EnableMultiVersionEnhance_Multi_Version_Quality_Badges", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnableMultiVersionEnhance_Show_quality_badges_and_sort_by_quality_on_multi_version_detail_pages__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableMultiVersionEnhance { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_MultiVersionDisplayOption_Version_Display_Option", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_MultiVersionDisplayOption_Version_label_source__SystemDefault_MediaInfo_or_FileNameWithoutExtension__Default_is_MediaInfo_", typeof(Resources))]
        [VisibleCondition(nameof(EnableMultiVersionEnhance), SimpleCondition.IsTrue)]
        [Required]
        public string MultiVersionDisplayOption { get; set; } = "MediaInfo";

        [DisplayNameL("ExperienceEnhanceOptions_MultiVersionSortOption_Version_Sort_Option", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_MultiVersionSortOption_Version_sort_order__SystemDefault_HighToLowQuality_LowToHighQuality_PathAscending_or_PathDescending__Default_is_SystemDefault_", typeof(Resources))]
        [VisibleCondition(nameof(EnableMultiVersionEnhance), SimpleCondition.IsTrue)]
        [Required]
        public string MultiVersionSortOption { get; set; } = "SystemDefault";

        [DisplayNameL("ExperienceEnhanceOptions_OptimizeSubtitle_Optimize_Subtitle", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_OptimizeSubtitle_Resolve_strm_media_path_to_local_mount_for_open_subtitles_hash_and_filename_search__requires_open_subtitles_plugin__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool OptimizeSubtitle { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_AutoMarkWatchedThreshold_Marked_Progress_Threshold", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_AutoMarkWatchedThreshold_Playback_progress_percentage_of_current_episode_required_to_mark_previous_episodes_as_watched__5_to_100__Default_is_90_", typeof(Resources))]
        [VisibleCondition(nameof(EnableAutoMarkWatched), SimpleCondition.IsTrue)]
        [Required]
        public int AutoMarkWatchedThreshold { get; set; } = 90;

        [DisplayNameL("ExperienceEnhanceOptions_AutoMarkWatchedExcludeFavorites_Exclude_Favorite_Episodes", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_AutoMarkWatchedExcludeFavorites_Favorite_episodes_will_not_be_automatically_marked_as_watched__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(EnableAutoMarkWatched), SimpleCondition.IsTrue)]
        [Required]
        public bool AutoMarkWatchedExcludeFavorites { get; set; } = false;

        [DisplayNameL("UIFunctionOptions_EditorTitle_UI_Functions", typeof(Resources))]
        public UIFunctionOptions UIFunctionOptions { get; set; } = new UIFunctionOptions();

        [Browsable(false)]
        public bool IsModSupported => RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
