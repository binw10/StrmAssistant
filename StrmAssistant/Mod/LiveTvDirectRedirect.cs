using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// LiveTV 直链：将电视源 MediaSource 标记为可直连，配合影音直链实现 302 直链，无需服务端转发。
    /// </summary>
    public class LiveTvDirectRedirect : PatchBase<LiveTvDirectRedirect>
    {
        private static MethodInfo _getChannelMediaSourcesMethod;

        public LiveTvDirectRedirect()
        {
            try
            {
                Initialize();

                if (IsEnabled && PatchTracker.FallbackPatchApproach != PatchApproach.None)
                {
                    Patch();
                }
            }
            catch (Exception e)
            {
                // 兜底：单个模块初始化失败不得影响插件加载与设置页
                try
                {
                    PatchTracker.FallbackPatchApproach = PatchApproach.None;
                    Plugin.Instance?.Logger?.Warn($"LiveTvDirectRedirect 构造失败，已禁用该功能: {e.Message}");
                }
                catch
                {
                }
            }
        }

        private static bool IsEnabled =>
            Plugin.Instance?.NetworkProxyStore?.GetOptions()?.DirectLink?.EnableLiveTvDirectRedirect ?? false;

        protected override void OnInitialize()
        {
            try
            {
                var assembly = Assembly.Load("Emby.LiveTV");
                var liveTvManagerType = assembly?.GetType("Emby.LiveTV.LiveTvManager", false);

                _getChannelMediaSourcesMethod = liveTvManagerType?.GetMethod("GetChannelMediaSources",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(BaseItem), typeof(CancellationToken) }, null);
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.Warn($"LiveTvDirectRedirect 初始化异常: {e.Message}");
            }

            if (_getChannelMediaSourcesMethod == null)
            {
                PatchTracker.FallbackPatchApproach = PatchApproach.None;
                Plugin.Instance.Logger.Warn("LiveTvDirectRedirect: 未命中 LiveTvManager.GetChannelMediaSources，LiveTV 直链不可用");
                return;
            }

            Plugin.Instance.Logger.Info("LiveTvDirectRedirect: LiveTV 直链目标方法已就绪");
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _getChannelMediaSourcesMethod,
                postfix: nameof(GetChannelMediaSourcesPostfix));
        }

        /// <summary>
        /// 保存选项后即时生效：按新选项应用/撤销补丁。
        /// </summary>
        public void ApplyOptionsAndConfigure(Options.DirectLinkOptions directLinkOptions)
        {
            if (PatchTracker.FallbackPatchApproach == PatchApproach.None) return;

            if (directLinkOptions.EnableLiveTvDirectRedirect)
            {
                Patch();
            }
            else
            {
                Unpatch();
            }
        }

        /// <summary>
        /// 换包模式：把「标记可直连」包成新 Task 交回调用方。
        /// <para>
        /// 不能用 <c>async void</c>——那会在第一个 <c>await</c> 处立即返回，Harmony 把**原始** Task
        /// 交给调用方，而调用方拿到 List 后可能已经开始生成播放地址，此时
        /// <c>RequiresOpening</c> / <c>SupportsDirectPlay</c> 是否已赋值取决于线程调度顺序，
        /// 表现为 LiveTV 直链"时好时坏"。
        /// </para>
        /// <para>
        /// 改成 <c>ref</c> + 返回新 Task 后，调用方 <c>await</c> 到的一定是**已改过**的同一个 List 实例。
        /// </para>
        /// </summary>
        [HarmonyPostfix]
        private static void GetChannelMediaSourcesPostfix(ref Task<List<MediaSourceInfo>> __result)
        {
            if (!IsEnabled || __result == null) return;

            __result = MarkDirectPlayableAsync(__result);
        }

        private static async Task<List<MediaSourceInfo>> MarkDirectPlayableAsync(
            Task<List<MediaSourceInfo>> task)
        {
            // 原 Task 若失败/取消，异常原样向外传播（不吞）
            var sources = await task.ConfigureAwait(false);

            try
            {
                if (sources != null)
                {
                    foreach (var mediaSource in sources)
                    {
                        mediaSource.RequiresOpening = false;
                        mediaSource.RequiresClosing = false;
                        mediaSource.OpenToken = null;
                        mediaSource.IsRemote = true;
                        mediaSource.Protocol = MediaProtocol.Http;
                        mediaSource.SupportsDirectPlay = true;
                        mediaSource.SupportsDirectStream = true;
                        mediaSource.SupportsTranscoding = false;
                    }
                }
            }
            catch (Exception e)
            {
                // 该路径低频，失败必须可见（此前只记 Debug，线上等于静默）
                Plugin.Instance.Logger.Warn($"LiveTvDirectRedirect: 标记 MediaSource 失败 - {e}");
            }

            return sources;
        }
    }
}