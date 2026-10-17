using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 体验增强 · 通知分组：剧集通知显示集详情，以及触发通知的元数据字段。
    /// 注意：<see cref="VisibleConditionAttribute"/> / <see cref="EnabledConditionAttribute"/> 的目标属性
    /// 按**同级路径**解析，<c>IsModSupported</c> 必须留在本类内。
    /// </summary>
    public class ExperienceNotificationOptions : EditableOptionsBase
    {
        [DisplayNameL("ExperienceEnhanceOptions_Group_Notification", typeof(Resources))]
        public override string EditorTitle => Resources.ExperienceEnhanceOptions_Group_Notification;

        [DisplayNameL("ExperienceEnhanceOptions_EnhanceNotification_Enhance_Notification", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_EnhanceNotification_Show_episode_details_in_series_notification__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool EnhanceNotificationSystem { get; set; } = false;

        [DisplayNameL("ExperienceEnhanceOptions_MetadataUpdateTrackedFields_Metadata_Update_Tracked_Fields", typeof(Resources))]
        [DescriptionL("ExperienceEnhanceOptions_MetadataUpdateTrackedFields_Comma_separated_metadata_fields_that_trigger_the_metadata_update_notification__blank_notifies_all__Default_is_EMPTY_", typeof(Resources))]
        [VisibleCondition(nameof(EnhanceNotificationSystem), SimpleCondition.IsTrue)]
        public string MetadataUpdateTrackedFields { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
