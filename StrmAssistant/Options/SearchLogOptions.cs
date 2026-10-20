using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 体验增强 · 搜索与日志分组：搜索结果卡片信息、搜索页推荐区块、服务器日志列表顺序。
    /// 作用面是「搜索页与服务器日志页」——最外围，故排在最后。
    /// </summary>
    public class SearchLogOptions : EditableOptionsBase
    {
        [DisplayNameL("SearchLogOptions_EditorTitle_Search_and_Logs", typeof(Resources))]
        public override string EditorTitle => Resources.SearchLogOptions_EditorTitle_Search_and_Logs;

        [DisplayNameL("ModOptions_ShowMediaInfo_Show_Media_Info", typeof(Resources))]
        [DescriptionL("ModOptions_ShowMediaInfo_Show_media_info_on_search_result_items__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool ShowMediaInfo { get; set; } = false;

        [DisplayNameL("UIFunctionOptions_BlockSearchRecommend_Block_Search_Recommendations", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_BlockSearchRecommend_Hide_the_recommendations_section_on_the_search_page__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool BlockSearchRecommend { get; set; } = false;

        [DisplayNameL("UIFunctionOptions_ReverseLogLines_Logs_Newest_First", typeof(Resources))]
        [DescriptionL("UIFunctionOptions_ReverseLogLines_List_server_log_files_from_newest_to_oldest__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool ReverseLogLines { get; set; } = false;
    }
}
