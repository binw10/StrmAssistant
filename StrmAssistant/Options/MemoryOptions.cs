using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 通用选项 · 内存管理分组：内存超限自动重启与周期性清理。
    /// 注意：<see cref="VisibleConditionAttribute"/> 的目标属性按**同级路径**解析。
    /// </summary>
    public class MemoryOptions : EditableOptionsBase
    {
        [DisplayNameL("GeneralOptions_Group_Memory", typeof(Resources))]
        public override string EditorTitle => Resources.GeneralOptions_Group_Memory;

        [DisplayNameL("PluginOptions_EnableMemoryAutoRestart_Auto_Restart_On_Memory_Limit", typeof(Resources))]
        [DescriptionL("PluginOptions_EnableMemoryAutoRestart_Automatically_restart_the_server_after_working_set_exceeds_the_limit_sustained__Admins_get_a_countdown_notification__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableMemoryAutoRestart { get; set; } = false;

        [DisplayNameL("PluginOptions_MemoryAutoRestartThresholdMB_Memory_Limit", typeof(Resources))]
        [DescriptionL("PluginOptions_MemoryAutoRestartThresholdMB_Working_set_limit_in_MB_before_restart_countdown_starts__Default_is_4096_", typeof(Resources))]
        [Required, MinValue(512), MaxValue(65536)]
        [VisibleCondition(nameof(EnableMemoryAutoRestart), SimpleCondition.IsTrue)]
        public int MemoryAutoRestartThresholdMB { get; set; } = 4096;

        [DisplayNameL("GeneralOptions_EnableMemoryCleanup_Title", typeof(Resources))]
        [DescriptionL("GeneralOptions_EnableMemoryCleanup_Description", typeof(Resources))]
        [Required]
        public bool EnableMemoryCleanup { get; set; } = true;

        [DisplayNameL("GeneralOptions_MemoryCleanupInterval_Title", typeof(Resources))]
        [DescriptionL("GeneralOptions_MemoryCleanupInterval_Description", typeof(Resources))]
        [VisibleCondition(nameof(EnableMemoryCleanup), SimpleCondition.IsTrue)]
        [Required, MinValue(1), MaxValue(120)]
        public int MemoryCleanupIntervalMinutes { get; set; } = 5;
    }
}
