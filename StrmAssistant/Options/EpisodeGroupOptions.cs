using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 元数据增强 · 剧集组分组：MovieDb 剧集组（Episode Group）刮削与扩展信息。
    /// </summary>
    public class EpisodeGroupOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataEnhanceOptions_Group_EpisodeGroup", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataEnhanceOptions_Group_EpisodeGroup;

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

        [Browsable(false)]
        public bool IsMovieDbPluginLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MovieDb") && IsModSupported;

        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
