using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI.Views;
using StrmAssistant.Options.Store;
using StrmAssistant.Options.UIBaseClasses.Views;
using System.Threading.Tasks;

namespace StrmAssistant.Options.View
{
    internal class MetadataEnhancePageView : PluginPageView
    {
        private readonly MetadataEnhanceOptionsStore _store;

        public MetadataEnhancePageView(PluginInfo pluginInfo, MetadataEnhanceOptionsStore store)
            : base(pluginInfo.Id)
        {
            _store = store;
            ContentData = store.GetOptions();
            MetadataEnhanceOptions.Initialize();
        }

        public MetadataEnhanceOptions MetadataEnhanceOptions => ContentData as MetadataEnhanceOptions;

        public override Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
        {
            if (ContentData is MetadataEnhanceOptions options)
            {
                options.ValidateOrThrow();
            }

            _store.SetOptions(MetadataEnhanceOptions);
            return base.OnSaveCommand(itemId, commandId, data);
        }

        public override Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
        {
            if (commandId == "ClearSeasonMap")
            {
                // 季映射清理是毫秒级操作（只清内存字典 + 删 json），同步执行即可
                try
                {
                    var cleared = Plugin.DoubanAssist.ClearSeasonMap();
                    Plugin.Instance.Logger.Info(
                        $"MetadataEnhance - Clear season map requested from UI: {cleared} season(s) removed");
                }
                catch (System.Exception e)
                {
                    Plugin.Instance.Logger.Warn("MetadataEnhance - Clear season map failed: " + e.Message);
                }

                return Task.FromResult<IPluginUIView>(this);
            }

            return base.RunCommand(itemId, commandId, data);
        }
    }
}
