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
            Plugin.Instance?.MainOptionsStore?.GetOptions()?.ModOptions?.DirectLinkOptions?.EnableLiveTvDirectRedirect ?? false;

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

        [HarmonyPostfix]
        private static async void GetChannelMediaSourcesPostfix(Task<List<MediaSourceInfo>> __result)
        {
            if (!IsEnabled || __result == null) return;

            try
            {
                foreach (var mediaSource in await __result)
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
            catch (Exception e)
            {
                Plugin.Instance.Logger.Debug($"LiveTvDirectRedirect: 处理 MediaSource 失败 - {e.Message}");
            }
        }
    }
}