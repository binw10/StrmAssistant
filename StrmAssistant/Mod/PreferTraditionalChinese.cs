using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using StrmAssistant.Common;
using System;
using System.Linq;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 优先繁中：MovieDb 刮削回填前把简体中文概要转为繁体。
    /// 供备选语言场景使用——TMDB 缺繁中数据回退简中时，写入前统一转繁。
    /// 复用 ClearTagline 的 Task 续体 postfix 模式。
    /// </summary>
    public class PreferTraditionalChinese : PatchBase<PreferTraditionalChinese>
    {
        private static System.Reflection.MethodInfo _movieGetMetadata;
        private static System.Reflection.MethodInfo _seriesGetMetadata;

        public PreferTraditionalChinese()
        {
            Initialize();

            if (Plugin.Instance.MetadataEnhanceStore.GetOptions().ChineseMetadata.PreferTraditionalChinese)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            var movieDbAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "MovieDb");
            if (movieDbAssembly == null) return;

            _movieGetMetadata = movieDbAssembly.GetType("MovieDb.MovieDbProvider")?.GetMethod("GetMetadata");
            _seriesGetMetadata = movieDbAssembly.GetType("MovieDb.MovieDbSeriesProvider")?.GetMethod("GetMetadata");

            if (Plugin.Instance.DebugMode)
            {
                Plugin.Instance.Logger.Debug(
                    $"PreferTraditionalChinese: MovieGetMetadata={_movieGetMetadata != null}, " +
                    $"SeriesGetMetadata={_seriesGetMetadata != null}");
            }
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _movieGetMetadata, postfix: nameof(MovieGetMetadataPostfix));
            PatchUnpatch(PatchTracker, apply, _seriesGetMetadata, postfix: nameof(SeriesGetMetadataPostfix));
        }

        [HarmonyPostfix]
        private static void MovieGetMetadataPostfix(ref Task<MetadataResult<Movie>> __result)
        {
            if (__result == null) return;

            __result = ConvertOverviewAsync(__result);
        }

        [HarmonyPostfix]
        private static void SeriesGetMetadataPostfix(ref Task<MetadataResult<Series>> __result)
        {
            if (__result == null) return;

            __result = ConvertOverviewAsync(__result);
        }

        /// <summary>
        /// 换包模式：<c>await</c> 原 Task → 繁中转换 → 原样返回。
        /// 相比 <c>ContinueWith</c>，异常与取消状态原样传播（见 <see cref="ClearTagline"/> 同类说明）。
        /// </summary>
        private static async Task<MetadataResult<T>> ConvertOverviewAsync<T>(Task<MetadataResult<T>> task)
            where T : BaseItem
        {
            var result = await task.ConfigureAwait(false);
            ConvertOverview(result);
            return result;
        }

        private static void ConvertOverview<T>(MetadataResult<T> result) where T : BaseItem
        {
            if (result?.Item == null) return;

            var overview = result.Item.Overview;
            if (string.IsNullOrWhiteSpace(overview) || !LanguageUtility.IsChinese(overview)) return;

            try
            {
                result.Item.Overview = LanguageUtility.ConvertSimplifiedToTraditional(overview);
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("PreferTraditionalChinese - Convert failed: " + ex.Message);
            }
        }
    }
}
