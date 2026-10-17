using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Mod;
using StrmAssistant.Properties;
using System;
using System.ComponentModel;
using System.Linq;

namespace StrmAssistant.Options
{
    public class PluginOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_EditorTitle_Basic_Settings", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_EditorTitle_Basic_Settings;

        public override string EditorDescription => string.Empty;

        public ButtonItem DisclaimerButton { get; set; } = new ButtonItem
        {
            Icon = IconNames.privacy_tip, Data1 = "DisclaimerDialog"
        };

        /// <summary>品牌信息：插件名组头 + 版本/支持版本行，位于免责声明之下、总开关之上。</summary>
        [DisplayNameL("PluginOptions_EditorTitle_Strm_Assistant", typeof(Resources))]
        public BrandOptions BrandOptions { get; set; } = new BrandOptions();

        /// <summary>
        /// 插件总开关。关闭后 <c>Plugin.Initialize</c> 会整段跳过（不 patch、不订阅事件、
        /// 不初始化队列），定时任务与 Web 端点也会早退；修改后需重启服务器才生效。
        /// 之所以不做即时生效，是因为要撤销 36 个 Harmony patch 并逐个验证幂等性，
        /// 一旦有遗漏会出现「关了其实没关」——比没有开关更糟。
        /// </summary>
        [DisplayNameL("PluginOptions_EnablePlugin_Enable_Plugin", typeof(Resources))]
        [DescriptionL("PluginOptions_EnablePlugin_Description", typeof(Resources))]
        [Required]
        public bool EnablePlugin { get; set; } = true;

        [VisibleCondition(nameof(ShowConflictPluginLoadedStatus), SimpleCondition.IsTrue)]
        public StatusItem ConflictPluginLoadedStatus { get; set; } = new StatusItem();

        [VisibleCondition(nameof(IsModSuccess), SimpleCondition.IsFalse)]
        public StatusItem ModStatus { get; set; } = new StatusItem();

        [DisplayNameL("GeneralOptions_EditorTitle_General_Options", typeof(Resources))]
        public GeneralOptions GeneralOptions { get; set; } = new GeneralOptions();

        [DisplayNameL("PluginOptions_ModOptions_Mod_Features", typeof(Resources))]
        public ModOptions ModOptions { get; set; } = new ModOptions();

        [DisplayNameL("AboutOptions_EditorTitle_About", typeof(Resources))]
        public AboutOptions AboutOptions { get; set; } = new AboutOptions();

        [Browsable(false)]
        public bool IsModSuccess => PatchManager.IsModSuccess();

        [Browsable(false)]
        public bool ShowConflictPluginLoadedStatus =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetName().Name)
                .Any(n => n == "StrmExtract" || n == "InfuseSync");

        public void Initialize()
        {
            BrandOptions.Initialize();
            DisclaimerButton.Caption = Resources.DisclaimerButtonText;

            if (ShowConflictPluginLoadedStatus)
            {
                ConflictPluginLoadedStatus.Caption = Resources
                    .PluginOptions_IncompatibleMessage_Please_uninstall_the_conflict_plugin_Strm_Extract;
                ConflictPluginLoadedStatus.StatusText = string.Empty;
                ConflictPluginLoadedStatus.Status = ItemStatus.Warning;
            }
            else
            {
                ConflictPluginLoadedStatus.Caption = string.Empty;
                ConflictPluginLoadedStatus.StatusText = string.Empty;
                ConflictPluginLoadedStatus.Status = ItemStatus.None;
            }

            if (IsModSuccess is false)
            {
                ModStatus.Caption = Resources.PluginOptions_IsHarmonyModFailed_Harmony_Mod_Failed;
                ModStatus.StatusText = string.Empty;
                ModStatus.Status = ItemStatus.Warning;
            }
            else
            {
                ModStatus.Caption = string.Empty;
                ModStatus.StatusText = string.Empty;
                ModStatus.Status = ItemStatus.None;
            }
        }
    }
}
