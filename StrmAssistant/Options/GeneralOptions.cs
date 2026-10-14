using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using Emby.Web.GenericEdit.Validation;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class GeneralOptions : EditableOptionsBase
    {
        [DisplayNameL("GeneralOptions_EditorTitle_General_Options", typeof(Resources))]
        public override string EditorTitle => Resources.GeneralOptions_EditorTitle_General_Options;

        [DisplayNameL("PluginOptions_CatchupMode_Catch_up_Mode__Experimental_", typeof(Resources))]
        [DescriptionL("PluginOptions_CatchupMode_Catch_up_users_favorites__exclusive_to_Strm___Default_is_False_", typeof(Resources))]
        [Required]
        public bool CatchupMode { get; set; } = false;

        [DisplayNameL("PluginOptions_CatchupMinWaitMinutes_Min_Wait_After_Library_Insert", typeof(Resources))]
        [DescriptionL("PluginOptions_CatchupMinWaitMinutes_Minutes_to_wait_after_an_item_enters_the_library_before_catch_up_processes_it__letting_initial_scraping_finish__Default_is_0_", typeof(Resources))]
        [Required, MinValue(0), MaxValue(120)]
        [VisibleCondition(nameof(CatchupMode), SimpleCondition.IsTrue)]
        public int CatchupMinWaitMinutes { get; set; } = 0;

        [DisplayNameL("PluginOptions_EnableMemoryAutoRestart_Auto_Restart_On_Memory_Limit", typeof(Resources))]
        [DescriptionL("PluginOptions_EnableMemoryAutoRestart_Automatically_restart_the_server_after_working_set_exceeds_the_limit_sustained__Admins_get_a_countdown_notification__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool EnableMemoryAutoRestart { get; set; } = false;

        [DisplayNameL("PluginOptions_MemoryAutoRestartThresholdMB_Memory_Limit", typeof(Resources))]
        [DescriptionL("PluginOptions_MemoryAutoRestartThresholdMB_Working_set_limit_in_MB_before_restart_countdown_starts__Default_is_4096_", typeof(Resources))]
        [Required, MinValue(512), MaxValue(65536)]
        [VisibleCondition(nameof(EnableMemoryAutoRestart), SimpleCondition.IsTrue)]
        public int MemoryAutoRestartThresholdMB { get; set; } = 4096;

        [DisplayNameL("GeneralOptions_RuntimeStatus_Runtime_Status", typeof(Resources))]
        public GenericItemList RuntimeStatusList { get; set; } = new GenericItemList();

        public ButtonItem CleanMemoryNowButton =>
            new ButtonItem(Resources.GeneralOptions_CleanMemoryNow_Clean_Memory_Now)
            {
                Icon = IconNames.memory, Data1 = "CleanMemoryNow"
            };

        public ButtonItem ScheduleIndexRebuildButton =>
            new ButtonItem(Resources.GeneralOptions_ScheduleIndexRebuild_Schedule_Search_Index_Rebuild)
            {
                Icon = IconNames.build, Data1 = "ScheduleIndexRebuild",
                ConfirmationPrompt = Resources.AreYouSureToContinue
            };

        public ButtonItem RestartServerButton =>
            new ButtonItem(Resources.GeneralOptions_RestartServer_Restart_Server)
            {
                Icon = IconNames.autorenew, Data1 = "RestartServer",
                ConfirmationPrompt = Resources.AreYouSureToContinue
            };

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

        [DisplayNameL("GeneralOptions_EnableMemoryCleanup_Title", typeof(Resources))]
        [DescriptionL("GeneralOptions_EnableMemoryCleanup_Description", typeof(Resources))]
        [Required]
        public bool EnableMemoryCleanup { get; set; } = true;

        [DisplayNameL("GeneralOptions_MemoryCleanupInterval_Title", typeof(Resources))]
        [DescriptionL("GeneralOptions_MemoryCleanupInterval_Description", typeof(Resources))]
        [VisibleCondition(nameof(EnableMemoryCleanup), SimpleCondition.IsTrue)]
        [Required, MinValue(1), MaxValue(120)]
        public int MemoryCleanupIntervalMinutes { get; set; } = 5;

        protected override void Validate(ValidationContext context)
        {
            if (CatchupMode && CatchupTaskScope.Contains(CatchupTask.Fingerprint.ToString()) &&
                !Plugin.Instance.IntroSkipStore.GetOptions().UnlockIntroSkip)
            {
                context.AddValidationError(Resources.InvalidFingerprintCatchup);
            }
        }

        public void Initialize()
        {
            CatchupTaskList.Clear();

            foreach (Enum item in Enum.GetValues(typeof(CatchupTask)))
            {
                var selectOption = new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = Utility.GetEnumDescription(item),
                    IsEnabled = true,
                };

                CatchupTaskList.Add(selectOption);
            }
        }
    }
}
