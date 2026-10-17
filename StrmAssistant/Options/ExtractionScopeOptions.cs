using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Common;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 提取 · 提取范围分组：媒体库范围、额外内容，以及按关键词/容器跳过提取的排除规则。
    /// 注意：<see cref="VisibleConditionAttribute"/> / <see cref="EnabledConditionAttribute"/> 的目标属性
    /// 按**同级路径**解析，本组所需的 helper 必须留在本类内。
    /// </summary>
    public class ExtractionScopeOptions : EditableOptionsBase
    {
        [DisplayNameL("MediaInfoExtractOptions_Group_ExtractionScope", typeof(Resources))]
        public override string EditorTitle => Resources.MediaInfoExtractOptions_Group_ExtractionScope;

        [Browsable(false)]
        public List<EditorSelectOption> LibraryList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("PluginOptions_LibraryScope_Library_Scope", typeof(Resources))]
        [DescriptionL("PluginOptions_LibraryScope_Library_scope_to_extract__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(LibraryList))]
        public string LibraryScope { get; set; } = string.Empty;

        [DisplayNameL("PluginOptions_IncludeExtra_Include_Extra", typeof(Resources))]
        [DescriptionL("PluginOptions_IncludeExtra_Include_media_extras_to_extract__Default_is_False_", typeof(Resources))]
        [Required]
        public bool IncludeExtra { get; set; } = false;

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoExcludeKeywords_Media_Info_Exclude_Keywords", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoExcludeKeywords_Comma_separated_keywords_matched_against_the_file_path_to_skip_media_info_extraction__Default_is_EMPTY_", typeof(Resources))]
        public string MediaInfoExcludeKeywords { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_MediaInfoExcludeMediaContainers_Media_Info_Exclude_Media_Containers", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_MediaInfoExcludeMediaContainers_Comma_separated_container_names_or_extensions_to_skip_media_info_extraction__Default_is_EMPTY_", typeof(Resources))]
        public string MediaInfoExcludeMediaContainers { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_VideoThumbnailExcludeKeywords_Video_Thumbnail_Exclude_Keywords", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_VideoThumbnailExcludeKeywords_Comma_separated_keywords_matched_against_the_file_path_to_skip_video_thumbnail_extraction__Default_is_EMPTY_", typeof(Resources))]
        public string VideoThumbnailExcludeKeywords { get; set; } = string.Empty;

        [DisplayNameL("MediaInfoExtractOptions_VideoThumbnailExcludeMediaContainers_Video_Thumbnail_Exclude_Media_Containers", typeof(Resources))]
        [DescriptionL("MediaInfoExtractOptions_VideoThumbnailExcludeMediaContainers_Comma_separated_container_names_or_extensions_to_skip_video_thumbnail_extraction__Default_is_EMPTY_", typeof(Resources))]
        public string VideoThumbnailExcludeMediaContainers { get; set; } = string.Empty;

        public void Initialize(ILibraryManager libraryManager)
        {
            LibraryList.Clear();

            LibraryList.Add(new EditorSelectOption
            {
                Value = "-1",
                Name = Resources.Favorites,
                IsEnabled = true
            });

            var libraries = libraryManager.GetVirtualFolders();

            foreach (var item in libraries)
            {
                if (LibraryApi.ExcludedCollectionTypes.Contains(item.CollectionType))
                {
                    continue;
                }

                var selectOption = new EditorSelectOption
                {
                    Value = item.ItemId,
                    Name = item.Name,
                    IsEnabled = true,
                };

                LibraryList.Add(selectOption);
            }
        }
    }
}
