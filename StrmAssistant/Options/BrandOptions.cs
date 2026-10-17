using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Reflection;

namespace StrmAssistant.Options
{
    /// <summary>
    /// 品牌信息：组头显示插件名（与「任务与并发」同款组头样式），
    /// 内容为版本行（自动读取程序集版本）+ 支持版本，样式对齐 Pro（小钻石图标）。
    /// </summary>
    public class BrandOptions : EditableOptionsBase
    {
        [DisplayNameL("PluginOptions_EditorTitle_Strm_Assistant", typeof(Resources))]
        public override string EditorTitle => Resources.PluginOptions_EditorTitle_Strm_Assistant;

        public GenericItemList BrandItemList { get; set; } = new GenericItemList();

        public void Initialize()
        {
            BrandItemList.Clear();

            BrandItemList.Add(new GenericListItem
            {
                PrimaryText = "版本：v" + AboutOptions.GetVersion() +
                              "　Support " + Plugin.AdvertisedEmbyVersion + " +",
                // Emby 4.10 的 IconNames 枚举没有 diamond，用 grade（实心星标）作徽章效果
                Icon = IconNames.grade,
                IconMode = ItemListIconMode.SmallRegular
            });
        }
    }
}
