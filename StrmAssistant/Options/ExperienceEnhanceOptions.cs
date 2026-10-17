using Emby.Web.GenericEdit;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 体验增强设置根节点。
    /// 设置项按功能拆到 4 个分组类（多版本合并 / 多版本展示 / 播放与清理 / 通知），
    /// 外加既有的「界面功能」子组。本类只负责声明分组。
    /// 注意：分组的属性名与分组类名不同（避免实例成员遮蔽类型名导致初始化表达式无法解析）。
    /// </summary>
    public class ExperienceEnhanceOptions : EditableOptionsBase
    {
        [DisplayNameL("ExperienceEnhanceOptions_EditorTitle_Experience_Enhance", typeof(Resources))]
        public override string EditorTitle => Resources.ExperienceEnhanceOptions_EditorTitle_Experience_Enhance;

        /// <summary>多版本合并：同文件夹多版本自动合并、范围、分组上限与拆分操作。</summary>
        [DisplayNameL("ExperienceEnhanceOptions_Group_VersionMerge", typeof(Resources))]
        public VersionMergeOptions Merge { get; set; } = new VersionMergeOptions();

        /// <summary>多版本展示：详情页画质角标、标签来源与排序。</summary>
        [DisplayNameL("ExperienceEnhanceOptions_Group_VersionDisplay", typeof(Resources))]
        public VersionDisplayOptions Display { get; set; } = new VersionDisplayOptions();

        /// <summary>播放与清理：自动标记已看、深度删除、字幕路径优化。</summary>
        [DisplayNameL("ExperienceEnhanceOptions_Group_Playback", typeof(Resources))]
        public ExperiencePlaybackOptions Playback { get; set; } = new ExperiencePlaybackOptions();

        /// <summary>通知：剧集通知显示集详情与触发字段。</summary>
        [DisplayNameL("ExperienceEnhanceOptions_Group_Notification", typeof(Resources))]
        public ExperienceNotificationOptions Notification { get; set; } = new ExperienceNotificationOptions();

        /// <summary>界面功能：列表/详情页的展示类开关（保持原有子组）。</summary>
        [DisplayNameL("UIFunctionOptions_EditorTitle_UI_Functions", typeof(Resources))]
        public UIFunctionOptions UIFunctionOptions { get; set; } = new UIFunctionOptions();

        /// <summary>Store 在保存时用于判断 patch 是否可用（与各组内的同名 helper 独立）。</summary>
        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
