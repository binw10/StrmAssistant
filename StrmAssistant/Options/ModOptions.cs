using Emby.Web.GenericEdit;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 搜索与直链根节点：拆为 2 个分组——中文搜索 / 直链重定向。
    /// （区块标题由「Mod 功能」改为「搜索与直链」，原名称是开发者视角的 Mod = 修改，
    /// 用户无法从字面看出里面是搜索与直链。）
    /// </summary>
    public class ModOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_ModOptions_Mod_Features", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_ModOptions_Mod_Features;

        /// <summary>中文搜索：拼音/中文模糊搜索、搜索范围与匹配偏好。</summary>
        [DisplayNameL("ModOptions_Group_ChineseSearch", typeof(Resources))]
        public ChineseSearchOptions ChineseSearch { get; set; } = new ChineseSearchOptions();

        public void Initialize()
        {
            ChineseSearch.Initialize();
        }
    }
}
