using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 通用选项根节点：拆为 3 个分组——任务与并发 / 内存管理 / 运行状态与维护。
    /// 本类只负责声明分组与委派 <c>Initialize()</c> / <c>Validate()</c>。
    /// 注意：分组的属性名与分组类名不同（避免实例成员遮蔽类型名导致初始化表达式无法解析）。
    /// </summary>
    public class GeneralOptions : EditableOptionsBase
    {
        [DisplayNameL("GeneralOptions_EditorTitle_General_Options", typeof(Resources))]
        public override string EditorTitle => Resources.GeneralOptions_EditorTitle_General_Options;

        /// <summary>任务与并发：追剧模式、追剧范围、并发上限与冷却。</summary>
        [DisplayNameL("GeneralOptions_Group_TaskConcurrency", typeof(Resources))]
        public TaskConcurrencyOptions TaskConcurrency { get; set; } = new TaskConcurrencyOptions();

        /// <summary>内存管理：超限自动重启与周期性清理。</summary>
        [DisplayNameL("GeneralOptions_Group_Memory", typeof(Resources))]
        public MemoryOptions Memory { get; set; } = new MemoryOptions();

        /// <summary>运行状态与维护：只读状态列表 + 维护操作按钮。</summary>
        [DisplayNameL("GeneralOptions_Group_RuntimeStatus", typeof(Resources))]
        public RuntimeStatusOptions RuntimeStatus { get; set; } = new RuntimeStatusOptions();

        public void Initialize()
        {
            TaskConcurrency.Initialize();
            RuntimeStatus.Initialize();
        }

        protected override void Validate(ValidationContext context)
        {
            ValidateChildGroup(context, nameof(TaskConcurrency));
        }
    }
}
