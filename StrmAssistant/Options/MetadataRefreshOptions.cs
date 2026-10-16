using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 剧集元数据刷新范围（可多选）。
    /// </summary>
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
        [DescriptionL("EpisodeRefreshOption_DefaultSeasonName_Default_Season_Name", typeof(Resources))]
        DefaultSeasonName,
        [DescriptionL("EpisodeRefreshOption_ReplaceCapturedImage_Replace_Captured_Image", typeof(Resources))]
        ReplaceCapturedImage
    }

    /// <summary>
    /// 元数据增强 · 元数据刷新分组：剧集刷新范围 / 回溯天数 / 生效媒体库。
    /// </summary>
    public class MetadataRefreshOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataEnhanceOptions_Group_MetadataRefresh", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataEnhanceOptions_Group_MetadataRefresh;

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

        [Browsable(false)]
        public List<EditorSelectOption> EpisodeRefreshLibraryList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("MetadataEnhanceOptions_EpisodeRefreshLibraryScope_Episode_Refresh_Library_Scope", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EpisodeRefreshLibraryScope_Episode_refresh_library_scope__Blank_includes_all_TV_shows_libraries_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(EpisodeRefreshLibraryList))]
        public string EpisodeRefreshLibraryScope { get; set; } = string.Empty;

        public void Initialize()
        {
            // 剧集刷新媒体库范围选项（影片库 / 剧集库 / 混合内容库，即 CollectionType 为 movies、tvshows 或空）。
            // 消费端 LibraryApi.IsItemLibraryInScope 用 GetCollectionFolders(item).Id 匹配，故 Value 取 VirtualFolderInfo.ItemId。
            EpisodeRefreshLibraryList.Clear();

            try
            {
                var libraries = Plugin.LibraryManager?.GetVirtualFolders();

                if (libraries != null)
                {
                    foreach (var library in libraries)
                    {
                        var collectionType = library.CollectionType;

                        if (!string.IsNullOrEmpty(collectionType) &&
                            !string.Equals(collectionType, "movies", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(collectionType, "tvshows", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        EpisodeRefreshLibraryList.Add(new EditorSelectOption
                        {
                            Value = library.ItemId,
                            Name = library.Name,
                            IsEnabled = true,
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.Warn("EpisodeRefreshLibraryList init failed: " + e.Message);
            }

            Plugin.Instance.Logger.Info("EpisodeRefreshLibraryList: " + EpisodeRefreshLibraryList.Count +
                                        " library(ies) - " +
                                        (EpisodeRefreshLibraryList.Count > 0
                                            ? string.Join(", ", EpisodeRefreshLibraryList.Select(o => o.Name))
                                            : "none"));

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
    }
}
