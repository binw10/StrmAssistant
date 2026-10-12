using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 影音直链：视频/音乐 Strm 302 直链、LiveTV 直链、302 跟随与客户端黑名单。
    /// </summary>
    public class DirectLinkOptions : EditableOptionsBase
    {
        [DisplayNameL("DirectLinkOptions_EditorTitle_Direct_Link", typeof(Resources))]
        public override string EditorTitle => Resources.DirectLinkOptions_EditorTitle_Direct_Link;

        [DisplayNameL("ModOptions_EnableStrmDirectRedirect_Strm_302_Direct_Link", typeof(Resources))]
        [DescriptionL("ModOptions_EnableStrmDirectRedirect_Smart_direct_link_for_video_and_music_Strm_urls_without_server_relay__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsDirectRedirectSupported), SimpleCondition.IsTrue)]
        public bool EnableStrmDirectRedirect { get; set; } = false;

        [DisplayNameL("ModOptions_EnableLiveTvDirectRedirect_LiveTV_Direct_Link", typeof(Resources))]
        [DescriptionL("ModOptions_EnableLiveTvDirectRedirect_Smart_direct_link_for_LiveTV_source_urls_without_server_relay__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsDirectRedirectSupported), SimpleCondition.IsTrue)]
        public bool EnableLiveTvDirectRedirect { get; set; } = false;

        [DisplayNameL("ModOptions_StrmDirectRedirectFollow302_Follow_302_Redirect", typeof(Resources))]
        [DescriptionL("ModOptions_StrmDirectRedirectFollow302_Auto_resolve_the_real_302_url_of_cloud_drives__OFF_uses_the_original_url_in_the_strm_file__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnableStrmDirectRedirect), SimpleCondition.IsTrue)]
        public bool StrmDirectRedirectFollow302 { get; set; } = false;

        [DisplayNameL("ModOptions_StrmVideoDirectRedirectClientBlacklist_Video_Direct_Link_Client_Blacklist", typeof(Resources))]
        [DescriptionL("ModOptions_StrmVideoDirectRedirectClientBlacklist_Client_name_keyword_match__matched_clients_are_blocked_from_video_strm_direct_link__Separated_by_comma__semicolon_or_newline", typeof(Resources))]
        [VisibleCondition(nameof(EnableStrmDirectRedirect), SimpleCondition.IsTrue)]
        public string StrmVideoDirectRedirectClientBlacklist { get; set; } = string.Empty;

        [DisplayNameL("ModOptions_StrmAudioDirectRedirectClientBlacklist_Music_Direct_Link_Client_Blacklist", typeof(Resources))]
        [DescriptionL("ModOptions_StrmAudioDirectRedirectClientBlacklist_Client_name_keyword_match__matched_clients_are_blocked_from_music_strm_direct_link__Separated_by_comma__semicolon_or_newline", typeof(Resources))]
        [VisibleCondition(nameof(EnableStrmDirectRedirect), SimpleCondition.IsTrue)]
        public string StrmAudioDirectRedirectClientBlacklist { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsDirectRedirectSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}