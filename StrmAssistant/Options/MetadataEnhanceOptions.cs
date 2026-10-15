using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Common;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using static StrmAssistant.Common.CommonUtility;

namespace StrmAssistant.Options
{
    public enum RefreshPersonOption
    {
        Default,
        FullRefresh,
        NoAdult
    }

    public class MetadataEnhanceOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_MetadataEnhanceOptions_Metadata_Enhance", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_MetadataEnhanceOptions_Metadata_Enhance;

        [DisplayNameL("ModOptions_ChineseMovieDb_Chinese_MovieDb", typeof(Resources))]
        [DescriptionL("ModOptions_ChineseMovieDb_Optimize_MovieDb_for_Chinese_metadata__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool ChineseMovieDb { get; set; } = false;

        [Browsable(false)]
        public List<EditorSelectOption> LanguageList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("ModOptions_FallbackLanguages_Fallback_Languages", typeof(Resources))]
        [DescriptionL("ModOptions_FallbackLanguages_Fallback_languages__Default_is_zh_SG_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LanguageList))]
        [VisibleCondition(nameof(ChineseMovieDb), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        public string FallbackLanguages { get; set; } = "zh-sg";

        [DisplayNameL("MetadataEnhanceOptions_ChineseTvdb_Customize_Tvdb_Fallback_Language", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_ChineseTvdb_Try_to_get_Chinese_or_Japanese_metadata_from_Tvdb__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsTvdbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool ChineseTvdb { get; set; } = false;
        
        [Browsable(false)]
        public List<EditorSelectOption> TvdbLanguageList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("ModOptions_FallbackLanguages_Fallback_Languages", typeof(Resources))]
        [DescriptionL("ModOptions_FallbackLanguages_Fallback_languages__Default_is_zh_SG_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(TvdbLanguageList))]
        [VisibleCondition(nameof(ChineseTvdb), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsTvdbPluginLoaded), SimpleCondition.IsTrue)]
        public string TvdbFallbackLanguages { get; set; } = "zhtw,yue";

        [DisplayNameL("MetadataEnhanceOptions_BlockNonFallbackLanguage_Block_Non_Fallback_Language", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_BlockNonFallbackLanguage_Block_English_such_non_fallback_language_in_overview__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(ShowBlockNonFallbackLanguage), SimpleCondition.IsTrue)]
        public bool BlockNonFallbackLanguage { get; set; } = false;

        [Browsable(false)]
        public bool ShowBlockNonFallbackLanguage =>
            ChineseMovieDb && IsMovieDbPluginLoaded || ChineseTvdb && IsTvdbPluginLoaded;

        [DisplayNameL("MetadataEnhanceOptions_MovieDbEpisodeGroup_Support_MovieDb_Episode_Group", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_MovieDbEpisodeGroup_Support_MovieDb_episode_group_scrapping_for_TV_shows__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool MovieDbEpisodeGroup { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_LocalEpisodeGroup_Local_Episode_Group", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_LocalEpisodeGroup_Store_or_load_episode_group_info_to_from_JSON_file__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(MovieDbEpisodeGroup), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        public bool LocalEpisodeGroup { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_EnableMovieDbEpisodeGroupExtendedInfo_Episode_Group_Extended_Info", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EnableMovieDbEpisodeGroupExtendedInfo_Apply_custom_season_overview_and_air_date_fields_from_the_episode_group_json__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(MovieDbEpisodeGroup), SimpleCondition.IsTrue)]
        [Required]
        public bool EnableMovieDbEpisodeGroupExtendedInfo { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_OptimizeMovieDbEpisodeScraping_Optimize_MovieDb_Episode_Scraping", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_OptimizeMovieDbEpisodeScraping_Fetch_episode_metadata_by_season_to_reduce_TMDB_requests__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool OptimizeMovieDbEpisodeScraping { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_EnableDoubanAssist_Douban_Assist_Scraping", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EnableDoubanAssist_After_movie_refresh_fill_missing_Chinese_overview_genres_locations_from_Douban__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableDoubanAssist { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DoubanCookie_Douban_Cookie", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DoubanCookie_Optional_Douban_cookie_for_anti_crawler_verification__Default_is_empty_", typeof(Resources))]
        [VisibleCondition(nameof(EnableDoubanAssist), SimpleCondition.IsTrue)]
        public string DoubanCookie { get; set; } = string.Empty;

        [DisplayNameL("MetadataEnhanceOptions_DoubanRatingOverride_Override_Rating_With_Douban", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DoubanRatingOverride_Overwrite_CommunityRating_and_VoteCount_with_Douban_rating__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(EnableDoubanAssist), SimpleCondition.IsTrue)]
        [Required]
        public bool DoubanRatingOverride { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DoubanAssistScrapeScope_Douban_Assist_Scope", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DoubanAssistScrapeScope_Comma_separated_library_IDs_where_Douban_assist_applies__blank_applies_to_all_libraries__Default_is_EMPTY_", typeof(Resources))]
        [VisibleCondition(nameof(EnableDoubanAssist), SimpleCondition.IsTrue)]
        public string DoubanAssistScrapeScope { get; set; } = string.Empty;

        [DisplayNameL("MetadataEnhanceOptions_MdbListApiKey_MdbList_Api_Key", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_MdbListApiKey_Optional_MdbList_API_key_used_to_refresh_critic_ratings_Rotten_Tomatoes_Metacritic_alongside_Douban_rating_updates__Default_is_empty_", typeof(Resources))]
        [VisibleCondition(nameof(EnableDoubanAssist), SimpleCondition.IsTrue)]
        public string MdbListApiKey { get; set; } = string.Empty;

        [DisplayNameL("MetadataEnhanceOptions_ClearTagline_Clear_Tagline", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_ClearTagline_Clear_taglines_populated_by_the_MovieDb_provider_on_refresh__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool ClearTagline { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_PreferTraditionalChinese_Prefer_Traditional_Chinese", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_PreferTraditionalChinese_Convert_simplified_Chinese_overviews_from_the_MovieDb_provider_to_traditional_on_refresh__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool PreferTraditionalChinese { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DefaultNewLibraryTmdb_Default_New_Library_Tmdb", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DefaultNewLibraryTmdb_Prefer_TheMovieDb_over_Tvdb_as_the_default_fetcher_when_creating_new_libraries__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool DefaultNewLibraryTmdb { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DoubanCastChinese_Douban_Cast_Chinese_Names", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DoubanCastChinese_Replace_English_actor_names_with_Douban_Chinese_names_for_matched_items__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(EnableDoubanAssist), SimpleCondition.IsTrue)]
        [Required]
        public bool DoubanCastChinese { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DoubanPosterFallback_Douban_Poster_Fallback", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DoubanPosterFallback_Set_Douban_poster_as_primary_image_when_the_movie_has_none__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(EnableDoubanAssist), SimpleCondition.IsTrue)]
        [Required]
        public bool DoubanPosterFallback { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DisableMovieDbEpisodeScrapingFallback_Disable_Per_Episode_Fallback", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DisableMovieDbEpisodeScrapingFallback_Skip_per_episode_requests_when_season_data_is_empty__Default_is_OFF_", typeof(Resources))]
        [VisibleCondition(nameof(OptimizeMovieDbEpisodeScraping), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool DisableMovieDbEpisodeScrapingFallback { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_DisableEpisodeImageScraping_Disable_Episode_Image_Scraping", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_DisableEpisodeImageScraping_Skip_all_episode_image_requests_to_speed_up_library_import__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool DisableEpisodeImageScraping { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_EnhanceMovieDbPerson_Enhance_MovieDb_Person", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EnhanceMovieDbPerson_Import_season_cast_and_update_series_people__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool EnhanceMovieDbPerson { get; set; } = false;
        
        [Browsable(false)]
        [Required]
        public string RefreshPersonMode { get; set; } = RefreshPersonOption.Default.ToString();

        [DisplayNameL("ModOptions_OriginalPoster_Original_Poster", typeof(Resources))]
        [DescriptionL("ModOptions_OriginalPoster_Show_original_poster_based_on_original_language__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool PreferOriginalPoster { get; set; } = false;

        [DisplayNameL("ModOptions_PinyinSortName_Pinyin_Sort_Title", typeof(Resources))]
        [DescriptionL("ModOptions_PinyinSortName_Auto_generate_pinyin_initials_as_sort_title", typeof(Resources))]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool PinyinSortName { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_EnhanceNfoMetadata_Nfo_Metadata_Import_Enhanced", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EnhanceNfoMetadata_Add_support_to_import_actor_image_url__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsNfoMetadataPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool EnhanceNfoMetadata { get; set; } = false;
        
        public enum EpisodeRefreshOption
        {
            [DescriptionL("EpisodeRefreshOption_NoOverview_No_Overview", typeof(Resources))]
            NoOverview,
            [DescriptionL("EpisodeRefreshOption_NoImage_No_Image", typeof(Resources))]
            NoImage,
            [DescriptionL("EpisodeRefreshOption_NonChineseOverview_Non_Chinese_Overview", typeof(Resources))]
            NonChineseOverview,
            [DescriptionL("EpisodeRefreshOption_DefaultEpisodeName_Default_Episode_Name", typeof(Resources))]
            DefaultEpisodeName,
            [DescriptionL("EpisodeRefreshOption_ReplaceCapturedImage_Replace_Captured_Image", typeof(Resources))]
            ReplaceCapturedImage
        }

        [Browsable(false)]
        public List<EditorSelectOption> EpisodeRefreshOptionList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("MetadataEnhanceOptions_EpisodeRefreshScope_Episode_Metadata_Refresh_Scope", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EpisodeRefreshScope_Episode_refresh_scope_for_scheduled_task_and_catch_up__Default_is_no_overview_and_no_image_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(EpisodeRefreshOptionList))]
        public string EpisodeRefreshScope { get; set; } = string.Join(",", EpisodeRefreshOption.NoOverview.ToString(),
            EpisodeRefreshOption.NoImage.ToString());

        [DisplayNameL("MetadataEnhanceOptions_EpisodeRefreshLookBackDays_Episode_Refresh_Lookback_Days", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EpisodeRefreshLookbackDays_Episode_metadata_refresh_lookback_days__Default_is_365_", typeof(Resources))]
        [Required, MinValue(1)]
        public int EpisodeRefreshLookbackDays { get; set; } = 365;

        [DisplayNameL("MetadataEnhanceOptions_EnableAltMovieDbUrl", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool AltMovieDbConfig { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_AltMovieDbApiUrl_Alternative_MovieDb_Api_Url", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_AltMovieDbApiUrl_Default_alternative_is_https___api_tmdb_org", typeof(Resources))]
        [VisibleCondition(nameof(AltMovieDbConfig), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        public string AltMovieDbApiUrl { get; set; } = "https://api.tmdb.org";

        [DisplayNameL("MetadataEnhanceOptions_AltMovieDbImageUrl_Alternative_MovieDb_Image_Url", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_AltMovieDbImageUrl_No_default_alternative__Provide_by_yourself_", typeof(Resources))]
        [VisibleCondition(nameof(AltMovieDbConfig), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        public string AltMovieDbImageUrl { get; set; } = string.Empty;

        [DisplayNameL("MetadataEnhanceOptions_AltMovieDbApiKey_Alternative_MovieDb_Api_Key", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_AltMovieDbApiKey_Provide_your_own_MovieDb_Api_Key__Blank_uses_system_default_", typeof(Resources))]
        [VisibleCondition(nameof(AltMovieDbConfig), SimpleCondition.IsTrue)]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        public string AltMovieDbApiKey { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsMovieDbPluginLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MovieDb") && IsModSupported;

        [Browsable(false)]
        public bool IsTvdbPluginLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Tvdb") && IsModSupported;

        [Browsable(false)]
        public bool IsNfoMetadataPluginLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "NfoMetadata") && IsModSupported;

        [Browsable(false)]
        public bool IsModSupported => RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public void Initialize()
        {
            LanguageList.Clear();

            foreach (var language in LanguageUtility.MovieDbFallbackLanguages)
            {
                LanguageList.Add(new EditorSelectOption
                {
                    Value = language.ToLowerInvariant(),
                    Name = language,
                    IsEnabled = true
                });
            }

            TvdbLanguageList.Clear();

            foreach (var language in LanguageUtility.TvdbFallbackLanguages)
            {
                TvdbLanguageList.Add(new EditorSelectOption
                {
                    Value = language,
                    Name = language,
                    IsEnabled = true
                });
            }

            EpisodeRefreshOptionList.Clear();

            foreach (Enum item in Enum.GetValues(typeof(EpisodeRefreshOption)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = Utility.GetEnumDescription(item),
                    IsEnabled = true,
                };

                EpisodeRefreshOptionList.Add(selectOption);
            }
        }

        protected override void Validate(ValidationContext context)
        {
            string metadataOptionsErrors = null;

            foreach (var (value, isValid, errorResource) in new (string, Func<string, bool>, string)[]
                     {
                         (AltMovieDbApiUrl, IsValidHttpUrl,
                             Resources.InvalidAltMovieDbApiUrl),
                         (AltMovieDbImageUrl, IsValidHttpUrl,
                             Resources.InvalidAltMovieDbImageUrl),
                         (AltMovieDbApiKey, IsValidMovieDbApiKey,
                             Resources.InvalidAltMovieDbApiKey)
                     })
            {
                if (!string.IsNullOrWhiteSpace(value) && !isValid(value))
                {
                    metadataOptionsErrors = metadataOptionsErrors == null ? errorResource : $"{metadataOptionsErrors}; {errorResource}";
                }
            }

            if (!string.IsNullOrEmpty(metadataOptionsErrors))
            {
                context.AddValidationError(metadataOptionsErrors);
            }
        }
    }
}
