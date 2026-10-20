using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 通用选项 · 任务与并发分组：追剧模式、追剧范围、并发上限与冷却。
    /// 注意：<see cref="VisibleConditionAttribute"/> 的目标属性按**同级路径**解析，
    /// 因此 <c>CatchupMode</c> 与其依赖项必须留在同一组。
    /// </summary>
    public class TaskConcurrencyOptions : EditableOptionsBase
    {
        [DisplayNameL("GeneralOptions_Group_TaskConcurrency", typeof(Resources))]
        public override string EditorTitle => Resources.GeneralOptions_Group_TaskConcurrency;

        [DisplayNameL("PluginOptions_CatchupMode_Catch_up_Mode__Experimental_", typeof(Resources))]
        [DescriptionL("PluginOptions_CatchupMode_Catch_up_users_favorites__exclusive_to_Strm___Default_is_False_", typeof(Resources))]
        [Required]
        public bool CatchupMode { get; set; } = false;

        [DisplayNameL("PluginOptions_CatchupMinWaitSeconds_Min_Wait_After_Library_Insert__Seconds_", typeof(Resources))]
        [DescriptionL("PluginOptions_CatchupMinWaitSeconds_Seconds_to_wait_after_an_item_enters_the_library_before_catch_up_processes_it__letting_initial_scraping_finish__Default_is_0_", typeof(Resources))]
        [Required, MinValue(0), MaxValue(MaxCatchupMinWaitSeconds)]
        [VisibleCondition(nameof(CatchupMode), SimpleCondition.IsTrue)]
        public int CatchupMinWaitSeconds { get; set; } = 0;

        /// <summary>
        /// 旧版分钟级字段：仅用于升级时把已有配置换算成秒（UI 不展示）。
        /// 归一化后会被置 0，之后只用 <see cref="CatchupMinWaitSeconds"/>。
        /// </summary>
        [Browsable(false)]
        public int CatchupMinWaitMinutes { get; set; } = 0;

        /// <summary>生效值（秒）：新字段优先，回退旧分钟字段 ×60。</summary>
        [Browsable(false)]
        public int EffectiveCatchupMinWaitSeconds =>
            CatchupMinWaitSeconds > 0
                ? CatchupMinWaitSeconds
                : Math.Max(0, CatchupMinWaitMinutes) * 60;

        private const int MaxCatchupMinWaitSeconds = 7200;

        public enum CatchupTask
        {
            [DescriptionL("CatchupTask_MediaInfo_MediaInfo", typeof(Resources))]
            MediaInfo,
            [DescriptionL("CatchupTask_Fingerprint_Fingerprint", typeof(Resources))]
            Fingerprint,
            [DescriptionL("CatchupTask_IntroSkip_IntroSkip", typeof(Resources))]
            IntroSkip,
            [DescriptionL("CatchupTask_EpisodeRefresh_EpisodeRefresh", typeof(Resources))]
            EpisodeRefresh
        }

        [Browsable(false)]
        public List<EditorSelectOption> CatchupTaskList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("GeneralOptions_CatchupScope_Catchup_Scope", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(CatchupTaskList))]
        [VisibleCondition(nameof(CatchupMode), SimpleCondition.IsTrue)]
        public string CatchupTaskScope { get; set; } = CatchupTask.MediaInfo.ToString();

        [DisplayNameL("PluginOptions_MaxConcurrentCount_Max_Concurrent_Count", typeof(Resources))]
        [DescriptionL("PluginOptions_MaxConcurrentCount_Max_Concurrent_Count_must_be_between_1_to_10__Default_is_1_", typeof(Resources))]
        [Required, MinValue(1), MaxValue(20)]
        public int MaxConcurrentCount { get; set; } = 1;

        [DisplayNameL("GeneralOptions_CooldownSeconds_Cooldown_Time__Seconds___Default_is_0", typeof(Resources))]
        [DescriptionL("GeneralOptions_CooldownDurationSeconds_Applicable_to_single_thread_mode__Default_is_0_", typeof(Resources))]
        [VisibleCondition(nameof(MaxConcurrentCount), ValueCondition.IsEqual, 1)]
        [Required, MinValue(0), MaxValue(60)]
        public int CooldownDurationSeconds { get; set; } = 0;

        [DisplayNameL("GeneralOptions_Tier2MaxConcurrentCount_Tier_2_Max_Concurrent_Count", typeof(Resources))]
        [DescriptionL("GeneralOptions_Tier2MaxConcurrentCount_Refresh_metadata__subtitle__local_tasks__Default_is_1_", typeof(Resources))]
        [Required, MinValue(1), MaxValue(20)]
        public int Tier2MaxConcurrentCount { get; set; } = 1;

        protected override void Validate(ValidationContext context)
        {
            if (CatchupMode && CatchupTaskScope.Contains(CatchupTask.Fingerprint.ToString()) &&
                !Plugin.Instance.IntroSkipStore.GetOptions().Fingerprint.UnlockIntroSkip)
            {
                context.AddValidationError(Resources.InvalidFingerprintCatchup);
            }
        }

        public void Initialize()
        {
            // 旧版分钟级配置 → 秒级（一次性归一化，之后 minutes 归零）
            if (CatchupMinWaitSeconds <= 0 && CatchupMinWaitMinutes > 0)
            {
                CatchupMinWaitSeconds = Math.Min(MaxCatchupMinWaitSeconds, CatchupMinWaitMinutes * 60);
                CatchupMinWaitMinutes = 0;
            }

            CatchupTaskList.Clear();

            foreach (Enum item in Enum.GetValues(typeof(CatchupTask)))
            {
                CatchupTaskList.Add(new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = Utility.GetEnumDescription(item),
                    IsEnabled = true,
                });
            }
        }
    }
}
