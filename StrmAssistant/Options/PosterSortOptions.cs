using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 元数据增强 · 海报与排序分组：原语言海报、拼音排序名、NFO 增强导入、清除宣传语。
    /// </summary>
    public class PosterSortOptions : EditableOptionsBase
    {
        [DisplayNameL("MetadataEnhanceOptions_Group_PosterSort", typeof(Resources))]
        public override string EditorTitle => Resources.MetadataEnhanceOptions_Group_PosterSort;

        [DisplayNameL("ModOptions_OriginalPoster_Original_Poster", typeof(Resources))]
        [DescriptionL("ModOptions_OriginalPoster_Show_original_poster_based_on_original_language__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool PreferOriginalPoster { get; set; } = false;

        [DisplayNameL("ModOptions_PinyinSortName_Pinyin_Sort_Title", typeof(Resources))]
        [DescriptionL("ModOptions_PinyinSortName_Auto_generate_pinyin_initials_as_sort_title", typeof(Resources))]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool PinyinSortName { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_EnhanceNfoMetadata_Nfo_Metadata_Import_Enhanced", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_EnhanceNfoMetadata_Add_support_to_import_actor_image_url__Default_is_OFF_", typeof(Resources))]
        [EnabledCondition(nameof(IsNfoMetadataPluginLoaded), SimpleCondition.IsTrue)]
        [Required]
        public bool EnhanceNfoMetadata { get; set; } = false;

        [DisplayNameL("MetadataEnhanceOptions_ClearTagline_Clear_Tagline", typeof(Resources))]
        [DescriptionL("MetadataEnhanceOptions_ClearTagline_Clear_taglines_populated_by_the_MovieDb_provider_on_refresh__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsModSupported), SimpleCondition.IsTrue)]
        public bool ClearTagline { get; set; } = false;

        [Browsable(false)]
        public bool IsNfoMetadataPluginLoaded =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "NfoMetadata") && IsModSupported;

        [Browsable(false)]
        public bool IsModSupported =>
            RuntimeInformation.ProcessArchitecture == Architecture.X64 || RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }
}
