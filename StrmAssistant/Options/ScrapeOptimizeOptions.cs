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
    /// 元数据增强 · 刮削优化分组：按季批量取集信息、跳过逐集回退、跳过剧集图片请求。
    /// </summary>
    public class ScrapeOptimizeOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataEnhanceOptions_Group_ScrapeOptimize", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataEnhanceOptions_Group_ScrapeOptimize;

        [DisplayNameL("MetadataEnhanceOptions_OptimizeMovieDbEpisodeScraping_Optimize_MovieDb_Episode_Scraping", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_OptimizeMovieDbEpisodeScraping_Fetch_episode_metadata_by_season_to_reduce_TMDB_requests__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsMovieDbPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool OptimizeMovieDbEpisodeScraping { get; set; } = false;

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

        [Browsable(false)]
        public bool IsMovieDbPluginLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MovieDb") && IsModSupported;

        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
