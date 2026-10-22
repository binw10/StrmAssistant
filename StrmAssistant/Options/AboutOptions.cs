using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace StrmAssistant.Options
{
    public class AboutOptions : EditableOptionsBase
    {
        /// <summary>「跟随客户端」的取值。此时不强制 <c>Resources.Culture</c>，由请求线程的 CurrentUICulture 决定。</summary>
        public const string UICultureAuto = "auto";

        [DisplayNameL("AboutOptions_EditorTitle_About", typeof(Resources))]
        public override string EditorTitle => Resources.AboutOptions_EditorTitle_About;

        public GenericItemList VersionInfoList { get; set; } = new GenericItemList();

        [Browsable(false)]
        public List<EditorSelectOption> UICultureList { get; set; } = new List<EditorSelectOption>();

        /// <summary>
        /// 插件界面语言。<c>auto</c> = 跟随客户端请求语言；其余为固定语言。
        /// <para>
        /// 影响面：设置页 Tab 标题、通知文案，以及 <c>Resources.Culture</c> 被显式设定后
        /// 所有走 <c>Resources.*</c> 的属性文案（`DisplayNameL` / `DescriptionL` 走反射取静态属性）。
        /// </para>
        /// </summary>
        [DisplayNameL("AboutOptions_DefaultUICulture_Language", typeof(Resources))]
        [DescriptionL("AboutOptions_DefaultUICulture_Description", typeof(Resources))]
        [SelectItemsSource(nameof(UICultureList))]
        public string DefaultUICulture { get; set; } = UICultureAuto;

        /// <summary>
        /// 把选项值解析成 <see cref="CultureInfo"/>。<c>auto</c> / 空值 / 非法值一律返回 <c>null</c>，
        /// 表示"不强制"，由调用方或 ResourceManager 回退到当前线程文化。
        /// </summary>
        internal static CultureInfo ResolveCulture(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, UICultureAuto, System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                return CultureInfo.GetCultureInfo(value);
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// 按当前选项同步 <c>Resources.Culture</c>。设为 <c>null</c> 时 ResourceManager 会回退到
        /// 请求线程的 CurrentUICulture —— 这正是「跟随客户端」的实现方式。
        /// </summary>
        internal static void ApplyUICulture(string value)
        {
            Resources.Culture = ResolveCulture(value);
        }

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
            UICultureList.Clear();
            UICultureList.Add(new EditorSelectOption
            {
                Value = UICultureAuto,
                Name = Resources.AboutOptions_DefaultUICulture_Auto,
                IsEnabled = true
            });
            UICultureList.Add(new EditorSelectOption { Value = "zh-CN", Name = "简体中文", IsEnabled = true });
            UICultureList.Add(new EditorSelectOption { Value = "zh-Hant", Name = "繁體中文", IsEnabled = true });
            UICultureList.Add(new EditorSelectOption { Value = "en-US", Name = "English", IsEnabled = true });

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
