using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using StrmAssistant.Options.Store;
using StrmAssistant.Options.UIBaseClasses.Views;
using System.Threading.Tasks;

namespace StrmAssistant.Options.View
{
    internal class NetworkProxyPageView : PluginPageView
    {
        private readonly NetworkProxyOptionsStore _store;

        public NetworkProxyPageView(PluginInfo pluginInfo, NetworkProxyOptionsStore store)
            : base(pluginInfo.Id)
        {
            _store = store;
            ContentData = store.GetOptions();

            NetworkProxyOptions.Initialize();
        }

        public NetworkProxyOptions NetworkProxyOptions => ContentData as NetworkProxyOptions;

        public override Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            if (ContentData is NetworkProxyOptions options)
            {
                options.ValidateOrThrow();
            }

            _store.SetOptions(NetworkProxyOptions);
            return base.OnSaveCommand(itemId, commandId, data);
        }
    }
}
