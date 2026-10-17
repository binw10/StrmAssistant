using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using StrmAssistant.Options.Store;
using StrmAssistant.Options.UIBaseClasses;
using StrmAssistant.Properties;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StrmAssistant.Options.View
{
    internal class MainPageController : ControllerBase, IHasTabbedUIPages
    {
        private readonly PluginInfo _pluginInfo;
        private readonly PluginOptionsStore _mainOptionsStore;
        private readonly MediaBrowser.Model.Tasks.ITaskManager _taskManager;
        private readonly MediaBrowser.Controller.Session.ISessionManager _sessionManager;
        private readonly List<IPluginUIPageController> _tabPages = new List<IPluginUIPageController>();

        public MainPageController(PluginInfo pluginInfo, ILibraryManager libraryManager,
            PluginOptionsStore mainOptionsStore, MediaInfoExtractOptionsStore mediaInfoExtractOptionsStore,
            MetadataEnhanceOptionsStore metadataEnhanceOptionsStore,
            IntroSkipOptionsStore introSkipOptionsStore, ExperienceEnhanceOptionsStore experienceEnhanceOptionsStore,
            NetworkProxyOptionsStore networkProxyOptionsStore,
            MediaBrowser.Model.Tasks.ITaskManager taskManager,
            MediaBrowser.Controller.Session.ISessionManager sessionManager)
            : base(pluginInfo.Id)
        {
            _pluginInfo = pluginInfo;
            _mainOptionsStore = mainOptionsStore;
            _taskManager = taskManager;
            _sessionManager = sessionManager;

            PageInfo = new PluginPageInfo
            {
                Name = "Settings",
                EnableInMainMenu = true,
                // DisplayName 会同时用作侧边栏菜单项名字，保持品牌名；
                // Tab 标题由 _tabPages 各自提供（首页 Tab 为「基础设置」）。
                DisplayName = Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Assistant",
                    Plugin.Instance.DefaultUICulture),
                MenuIcon = "video_settings",
                IsMainConfigPage = false,
            };

            // Tab 顺序按使用频率排列：高级功能紧跟基础设置，其后是三大日常功能。
            // 注：Emby 强制渲染主页为第一个 Tab（标题=PageInfo.DisplayName），与侧边栏
            // 菜单名无法分离；曾实测显式注册主页 Tab，结果出现 7 个且重复，已回滚。
            _tabPages.Add(new TabPageController(pluginInfo, nameof(ExperienceEnhancePageView),
                Resources.ResourceManager.GetString("ExperienceEnhanceOptions_EditorTitle_Experience_Enhance",
                    Plugin.Instance.DefaultUICulture),
                e => new ExperienceEnhancePageView(pluginInfo, libraryManager, experienceEnhanceOptionsStore)));
            _tabPages.Add(new TabPageController(pluginInfo, nameof(MediaInfoExtractPageView),
                Resources.ResourceManager.GetString("PluginOptions_EditorTitle_Strm_Extract",
                    Plugin.Instance.DefaultUICulture),
                e => new MediaInfoExtractPageView(pluginInfo, libraryManager, mediaInfoExtractOptionsStore)));
            _tabPages.Add(new TabPageController(pluginInfo, nameof(MetadataEnhancePageView),
                Resources.ResourceManager.GetString("PluginOptions_MetadataEnhanceOptions_Metadata_Enhance",
                    Plugin.Instance.DefaultUICulture),
                e => new MetadataEnhancePageView(pluginInfo, metadataEnhanceOptionsStore)));
            _tabPages.Add(new TabPageController(pluginInfo, nameof(IntroSkipPageView),
                Resources.ResourceManager.GetString("PluginOptions_IntroSkipOptions_Intro_Credits_Detection",
                    Plugin.Instance.DefaultUICulture),
                e => new IntroSkipPageView(pluginInfo, libraryManager, introSkipOptionsStore)));
            _tabPages.Add(new TabPageController(pluginInfo, nameof(NetworkProxyPageView),
                Resources.ResourceManager.GetString("NetworkProxyOptions_EditorTitle_Network_Direct_Link",
                    Plugin.Instance.DefaultUICulture),
                e => new NetworkProxyPageView(pluginInfo, networkProxyOptionsStore)));
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new HomePageView(_pluginInfo, _mainOptionsStore, _taskManager, _sessionManager);
            return Task.FromResult(view);
        }

        public IReadOnlyList<IPluginUIPageController> TabPageControllers => _tabPages.AsReadOnly();
    }
}
