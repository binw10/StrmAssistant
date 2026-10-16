using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using System;
using System.Linq;
using System.Threading.Tasks;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 清标语：MovieDb 刮削结果回填前清空 Taglines，避免 TMDB 英文标语噪音。
    /// 通过 Task 续体在 GetMetadata 完成后修改结果（MovieDb 程序集由 Plugin 系统加载）。
    /// </summary>
    public class ClearTagline : PatchBase<ClearTagline>
    {
        private static System.Reflection.MethodInfo _movieGetMetadata;
        private static System.Reflection.MethodInfo _seriesGetMetadata;

        public ClearTagline()
        {
            Initialize();

            if (Plugin.Instance.MetadataEnhanceStore.GetOptions().PosterSort.ClearTagline)
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
                    $"ClearTagline: MovieGetMetadata={_movieGetMetadata != null}, SeriesGetMetadata={_seriesGetMetadata != null}");
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

            __result = __result.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion) ClearTaglines(t.Result);
                return t.Result;
            });
        }

        [HarmonyPostfix]
        private static void SeriesGetMetadataPostfix(ref Task<MetadataResult<Series>> __result)
        {
            if (__result == null) return;

            __result = __result.ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion) ClearTaglines(t.Result);
                return t.Result;
            });
        }

        private static void ClearTaglines<T>(MetadataResult<T> result) where T : BaseItem
        {
            if (result?.Item == null) return;

            if (!string.IsNullOrEmpty(result.Item.Tagline))
            {
                result.Item.Tagline = null;
            }
        }
    }
}
