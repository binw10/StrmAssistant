using Emby.Media.Common.Extensions;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 搜索与直链 · 中文搜索分组：拼音/中文模糊搜索、搜索范围与匹配偏好。
    /// 注意：<see cref="VisibleConditionAttribute"/> / <see cref="EnabledConditionAttribute"/> 的目标属性
    /// 按**同级路径**解析，<c>IsChineseSearchSupported</c> 必须留在本类内。
    /// </summary>
    public class ChineseSearchOptions : EditableOptionsBase
    {
        [DisplayNameL("ModOptions_Group_ChineseSearch", typeof(Resources))]
        public override string EditorTitle => Resources.ModOptions_Group_ChineseSearch;

        [DisplayNameL("ModOptions_EnhanceChineseSearch_Enhance_Chinese_Search", typeof(Resources))]
        [DescriptionL("ModOptions_EnhanceChineseSearch_Support_Chinese_fuzzy_search_and_Pinyin_search__Default_is_OFF_", typeof(Resources))]
        [Required]
        [EnabledCondition(nameof(IsChineseSearchSupported), SimpleCondition.IsTrue)]
        public bool EnhanceChineseSearch { get; set; } = false;

        [Browsable(false)]
        public bool EnhanceChineseSearchRestore { get; set; } = false;

        public enum SearchItemType
        {
            [DescriptionL("ItemType_Movie_Movie", typeof(Resources))] Movie,
            [DescriptionL("ItemType_Collection_Collection", typeof(Resources))] Collection,
            [DescriptionL("ItemType_Series_Series", typeof(Resources))] Series,
            [DescriptionL("ItemType_Season_Season", typeof(Resources))] Season,
            [DescriptionL("ItemType_Episode_Episode", typeof(Resources))] Episode,
            [DescriptionL("ItemType_Person_Person", typeof(Resources))] Person,
            [DescriptionL("ItemType_LiveTv_LiveTv", typeof(Resources))] LiveTv,
            [DescriptionL("ItemType_Music_Music", typeof(Resources))] Music,
            [DescriptionL("ItemType_MusicAlbum_MusicAlbum", typeof(Resources))] MusicAlbum,
            [DescriptionL("ItemType_MusicArtist_MusicArtist", typeof(Resources))] MusicArtist,
            [DescriptionL("ItemType_Playlist_Playlist", typeof(Resources))] Playlist,
            [DescriptionL("ItemType_Video_Video", typeof(Resources))] Video,
            [DescriptionL("ItemType_Photo_Photo", typeof(Resources))] Photo,
            [DescriptionL("ItemType_PhotoAlbum_PhotoAlbum", typeof(Resources))] PhotoAlbum,
            [DescriptionL("ItemType_Genre_Genre", typeof(Resources))] Genre,
            [DescriptionL("ItemType_Book_Book", typeof(Resources))] Book,
            [DescriptionL("ItemType_Game_Game", typeof(Resources))] Game,
            [DescriptionL("ItemType_Trailer_Trailer", typeof(Resources))] Trailer,
            [DescriptionL("ItemType_Tag_Tag", typeof(Resources))] Tag,
            [DescriptionL("ItemType_Studio_Studio", typeof(Resources))] Studio
        }

        [Browsable(false)]
        public List<EditorSelectOption> SearchItemTypeList { get; set; } = new List<EditorSelectOption>();

        [DisplayNameL("ModOptions_SearchScope_Search_Scope", typeof(Resources))]
        [DescriptionL("ModOptions_SearchScope_Include_item_types__Blank_includes_all_", typeof(Resources))]
        [EditMultilSelect]
        [SelectItemsSource(nameof(SearchItemTypeList))]
        [VisibleCondition(nameof(EnhanceChineseSearch), SimpleCondition.IsTrue)]
        public string SearchScope { get; set; } =
            string.Join(",", new[] { SearchItemType.Movie, SearchItemType.Collection, SearchItemType.Series });

        [DisplayNameL("ModOptions_ExcludeOriginalTitle_Exclude_Original_Title", typeof(Resources))]
        [DescriptionL("ModOptions_ExcludeOriginalTitle_Exclude_original_title_from_search__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnhanceChineseSearch), SimpleCondition.IsTrue)]
        public bool ExcludeOriginalTitleFromSearch { get; set; } = false;

        [DisplayNameL("ModOptions_DigitsAsTmdbId_Digits_As_Tmdb_Id", typeof(Resources))]
        [DescriptionL("ModOptions_DigitsAsTmdbId_Treat_a_pure_numeric_search_term_as_a_TMDB_id__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnhanceChineseSearch), SimpleCondition.IsTrue)]
        public bool DigitsAsTmdbId { get; set; } = false;

        [DisplayNameL("ModOptions_MixedChineseVariants_Mixed_Chinese_Variants", typeof(Resources))]
        [DescriptionL("ModOptions_MixedChineseVariants_Match_traditional_simplified_and_variant_Chinese_characters_interchangeably__Default_is_OFF_", typeof(Resources))]
        [Required]
        [VisibleCondition(nameof(EnhanceChineseSearch), SimpleCondition.IsTrue)]
        public bool MixedChineseVariants { get; set; } = false;

        [Browsable(false)]
        public bool IsChineseSearchSupported =>
            (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
             RuntimeInformation.ProcessArchitecture == Architecture.Arm64) &&
            Plugin.Instance.ApplicationHost.ApplicationVersion >= new Version("4.9.1.0");

        public void Initialize()
        {
            SearchItemTypeList.Clear();

            foreach (Enum item in Enum.GetValues(typeof(SearchItemType)))
            {
                SearchItemTypeList.Add(new EditorSelectOption
                {
                    Value = item.ToString(),
                    Name = Utility.GetEnumDescription(item),
                    IsEnabled = true,
                });
            }
        }
    }
}
