using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;
using System.Reflection;

namespace StrmAssistant.Options
{
    public class AboutOptions : EditableOptionsBase
    {
        [DisplayNameL("AboutOptions_EditorTitle_About", typeof(Resources))]
        public override string EditorTitle => Resources.AboutOptions_EditorTitle_About;

        public GenericItemList VersionInfoList { get; set; } = new GenericItemList();

        [Browsable(false)]
        public bool DebugMode { get; set; } = false;

        [Browsable(false)]
        public string GitHubToken { get; set; } = string.Empty;

        [Browsable(false)]
        public string GitHubProxy { get; set; } = string.Empty;
        
        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return version;
            return version.EndsWith(".0") ? version.Substring(0, version.Length - 2) : version;
        }

        /// <summary>
        /// 显示用版本号：只返回程序集版本（如 3.0.0.55），不再拼接 git commit hash。
        /// 供品牌区（BrandOptions）与关于页共用。
        /// </summary>
        internal static string GetVersion()
        {
            return NormalizeVersion(Assembly.GetExecutingAssembly().GetName().Version?.ToString()) ?? "0.0.0.0";
        }

        public void Initialize()
        {
            VersionInfoList.Clear();

            // 版本号已移至基础设置页顶部的品牌区（BrandOptions），此处仅保留链接。

            VersionInfoList.Add(
                new GenericListItem
                {
                    PrimaryText = Resources.Repo_Link,
                    Icon = IconNames.code,
                    IconMode = ItemListIconMode.SmallRegular,
                    HyperLink = "https://github.com/sjtuross/StrmAssistant",
                });

            VersionInfoList.Add(
                new GenericListItem
                {
                    PrimaryText = Resources.Wiki_Link,
                    Icon = IconNames.menu_book,
                    IconMode = ItemListIconMode.SmallRegular,
                    HyperLink = "https://github.com/sjtuross/StrmAssistant/wiki",
                });
        }
    }
}
