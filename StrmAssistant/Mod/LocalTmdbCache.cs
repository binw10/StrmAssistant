using HarmonyLib;
using MediaBrowser.Common.Net;
using StrmAssistant.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 本地 TMDB 被动缓存（v1）：在 MovieDb 的元数据 HTTP 唯一出口
    /// <c>MovieDbProviderBase.GetMovieDbResponse(HttpRequestOptions)</c> 上读写磁盘缓存。
    ///
    /// - 命中且允许读（缓存优先 / 仅用缓存）→ 直接返回缓存响应，跳过网络请求；
    /// - 未命中且允许写（缓存优先 / 在线并缓存）→ 原样请求，落盘后返回。
    ///
    /// 仅当 <see cref="Options.MetadataEnhanceOptions.IsMetadataCacheEnabled"/> 为真时才打补丁，
    /// 默认（模式 None）完全不介入，保证零行为变化。
    /// 图像 API（GetImageResponse，不带 language 参数）暂不缓存，留待 v2。
    /// </summary>
    public class LocalTmdbCache : PatchBase<LocalTmdbCache>
    {
        private static MethodInfo _getMovieDbResponse;

        public LocalTmdbCache()
        {
            Initialize();

            if (Plugin.Instance.MetadataEnhanceStore.GetOptions().IsMetadataCacheEnabled)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var movieDbAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "MovieDb");
                if (movieDbAssembly == null)
                {
                    Plugin.Instance.Logger.Info("LocalTmdbCache - MovieDb plugin not loaded, skip");
                    return;
                }

                var providerBase = movieDbAssembly.GetType("MovieDb.MovieDbProviderBase");
                _getMovieDbResponse = providerBase?.GetMethod("GetMovieDbResponse",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, new[] { typeof(HttpRequestOptions) }, null);

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"LocalTmdbCache: GetMovieDbResponse={_getMovieDbResponse != null}");
                }

                if (_getMovieDbResponse == null)
                {
                    Plugin.Instance.Logger.Warn(
                        "LocalTmdbCache - MovieDbProviderBase.GetMovieDbResponse not found; local TMDB cache inactive");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("LocalTmdbCache init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _getMovieDbResponse,
                prefix: nameof(GetMovieDbResponsePrefix), postfix: nameof(GetMovieDbResponsePostfix));
        }

        [HarmonyPrefix]
        private static bool GetMovieDbResponsePrefix(HttpRequestOptions options, ref Task<HttpResponseInfo> __result)
        {
            try
            {
                if (options?.Url is not { Length: > 0 } url) return true;
                if (!Plugin.Instance.MetadataEnhanceStore.GetOptions().IsMetadataCacheReadEnabled) return true;
                if (!LocalMetadataCache.TryGet(url, out var bytes)) return true;

                __result = Task.FromResult(LocalMetadataCache.BuildResponse(bytes, url));

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug($"LocalTmdbCache - HIT ({bytes.Length} bytes) {url}");
                }

                return false; // 跳过原方法（含网络请求）
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("LocalTmdbCache prefix failed: " + ex.Message);
                return true;
            }
        }

        [HarmonyPostfix]
        private static void GetMovieDbResponsePostfix(ref Task<HttpResponseInfo> __result, HttpRequestOptions options)
        {
            try
            {
                if (__result == null || options?.Url is not { Length: > 0 } url) return;
                if (!Plugin.Instance.MetadataEnhanceStore.GetOptions().IsMetadataCacheWriteEnabled) return;

                __result = CacheAndReturn(__result, url);
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("LocalTmdbCache postfix failed: " + ex.Message);
            }
        }

        private static async Task<HttpResponseInfo> CacheAndReturn(Task<HttpResponseInfo> inner, string url)
        {
            var response = await inner.ConfigureAwait(false);

            try
            {
                if (response?.Content != null)
                {
                    var bytes = LocalMetadataCache.ReadAll(response.Content);
                    if (bytes is { Length: > 0 })
                    {
                        LocalMetadataCache.Set(url, bytes);
                        // 原流已被读空，返回一个新的可读流供调用方反序列化
                        response.Content = new MemoryStream(bytes, false);

                        if (Plugin.Instance.DebugMode)
                        {
                            Plugin.Instance.Logger.Debug($"LocalTmdbCache - STORE ({bytes.Length} bytes) {url}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("LocalTmdbCache cache-write failed: " + ex.Message);
            }

            return response;
        }
    }
}
