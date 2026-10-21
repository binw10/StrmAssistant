using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 提取 · 结果与持久化分组：探测结果的清洗，以及 JSON 持久化 / 恢复模式。
    /// 注意：<see cref="VisibleConditionAttribute"/> 的目标属性按**同级路径**解析，
    /// 因此 <c>PersistMediaInfoMode</c> 与 <c>MediaInfoJsonRootFolder</c> 必须留在同一组。
    /// </summary>
    public class MediaInfoPersistOptions : EditableOptionsBase
    {
        [DisplayNameL("MediaInfoExtractOptions_Group_MediaInfoPersist", typeof(Resources))]
        public override string EditorTitle => Resources.MediaInfoExtractOptions_Group_MediaInfoPersist;

        [DisplayNameL("MediaInfoExtractOptions_ClearEmbeddedMetadata_Clear_Embedded_Metadata", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_ClearEmbeddedMetadata_Strip_embedded_metadata_from_movie_and_episode_probe_results__Default_is_ON_", typeof(Resources))]
        [Required]
        public bool ClearEmbeddedMetadata { get; set; } = true;

        [Browsable(false)]
        public List<EditorRadioOption> PersistMediaInfoOptionList { get; set; } = new List<EditorRadioOption>();

        [DisplayName("")]
        [SelectItemsSource(nameof(PersistMediaInfoOptionList))]
        [SelectShowRadioGroup]
        public string PersistMediaInfoMode { get; set; } =
            MediaInfoExtractOptions.PersistMediaInfoOption.None.ToString();

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoJsonRootFolder_MediaInfo_Json_Root_Folder", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoJsonRootFolder_Store_or_load_media_info_JSON_files_under_this_root_folder__Default_is_EMPTY_", typeof(Resources))]
        [EditFolderPicker]
        public string MediaInfoJsonRootFolder { get; set; } = string.Empty;

        public void Initialize()
        {
            PersistMediaInfoOptionList.Clear();
            PersistMediaInfoOptionList = new List<EditorRadioOption>
            {
                new EditorRadioOption
                {
                    Value = MediaInfoExtractOptions.PersistMediaInfoOption.Default,
                    PrimaryText = Resources.MediaInfoExtractOptions_PersistMediaInfo_Persist_MediaInfo,
                    SecondaryText =
                        Resources
                            .MediaInfoExtractOptions_PersistMediaInfo_Persist_media_info_in_JSON_file__Default_is_OFF_
                },
                new EditorRadioOption
                {
                    Value = MediaInfoExtractOptions.PersistMediaInfoOption.Restore,
                    PrimaryText = Resources.MediaInfoExtractOptions_MediaInfoRestoreMode_MediaInfo_Restore_Mode,
                    SecondaryText =
                        Resources
                            .MediaInfoExtractOptions_MediaInfoRestoreMode_Only_restore_media_info__chapters__and_video_thumbnails_from_JSON_or_BIF__skipping_extraction__Default_is_OFF_
                },
                new EditorRadioOption
                {
                    Value = MediaInfoExtractOptions.PersistMediaInfoOption.None,
                    PrimaryText = Resources.PersistMediaInfoOption_None_None
                }
            };
        }
    }
}
