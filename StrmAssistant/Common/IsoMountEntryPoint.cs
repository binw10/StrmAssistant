using System;
using MediaBrowser.Controller.Plugins;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 方案 4B：Emby 服务器关闭时释放全部 ISO 挂载的钩子。
    ///
    /// 该类型需被 Emby 注册为 server entry point 后，<see cref="Run"/> / <see cref="Dispose"/>
    /// 才会被调用——是否注册以日志实测确认（两方法均打 Info 日志）。
    /// 即便未被调用，方案 4A 的启动期对账仍可兜底释放遗留挂载。
    /// </summary>
    public class IsoMountEntryPoint : IServerEntryPoint
    {
        public void Run()
        {
            Plugin.Instance?.Logger?.Info("IsoExtraction - IsoMountEntryPoint.Run invoked");
        }

        public void Dispose()
        {
            Plugin.Instance?.Logger?.Info(
                "IsoExtraction - IsoMountEntryPoint.Dispose invoked, releasing all ISO mounts");
            IsoMounter.DismountAll();
        }
    }
}
