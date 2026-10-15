using HarmonyLib;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Configuration;
using System;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    /// <summary>
    /// 新建媒体库默认 TMDB：AddVirtualFolder API 完成后，把新库的元数据/图像刮削器中
    /// TheMovieDb 置顶（Emby 部分库类型默认 TVDB 优先）。
    /// </summary>
    public class DefaultTmdbLibrary : PatchBase<DefaultTmdbLibrary>
    {
        private static MethodInfo _addVirtualFolderPost;

        private static string _pendingLibraryName;

        public DefaultTmdbLibrary()
        {
            Initialize();

            if (Plugin.Instance.MetadataEnhanceStore.GetOptions().DefaultNewLibraryTmdb)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            try
            {
                var embyApi = Assembly.Load("Emby.Api");
                var libraryStructureService = embyApi.GetType("Emby.Api.Library.LibraryStructureService");
                var addVirtualFolderRequest = embyApi.GetType("Emby.Api.Library.AddVirtualFolder");

                _addVirtualFolderPost = libraryStructureService?.GetMethod("Post",
                    new[] { addVirtualFolderRequest });

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"DefaultTmdbLibrary: AddVirtualFolder Post={_addVirtualFolderPost != null}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("DefaultTmdbLibrary init failed: " + ex.Message);
            }
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _addVirtualFolderPost,
                prefix: nameof(AddVirtualFolderPrefix), postfix: nameof(AddVirtualFolderPostfix));
        }

        [HarmonyPrefix]
        private static void AddVirtualFolderPrefix(object request)
        {
            _pendingLibraryName = null;

            try
            {
                var name = request?.GetType().GetProperty("Name")?.GetValue(request) as string;
                if (!string.IsNullOrWhiteSpace(name)) _pendingLibraryName = name;
            }
            catch
            {
                // ignore
            }
        }

        [HarmonyPostfix]
        private static void AddVirtualFolderPostfix()
        {
            var name = _pendingLibraryName;
            _pendingLibraryName = null;

            if (string.IsNullOrWhiteSpace(name)) return;

            ApplyTmdbPreference(name);
        }

        private static void ApplyTmdbPreference(string libraryName)
        {
            try
            {
                var libraryManager = Plugin.LibraryManager;
                if (libraryManager == null) return;

                // 新建完成可能有短暂延迟，重试几次匹配库名
                var matched = default(MediaBrowser.Model.Entities.VirtualFolderInfo);
                for (var attempt = 0; attempt < 5 && matched == null; attempt++)
                {
                    if (attempt > 0) System.Threading.Thread.Sleep(300);

                    matched = libraryManager.GetVirtualFolders()
                        .FirstOrDefault(f => string.Equals(f.Name, libraryName, StringComparison.Ordinal));
                }

                if (matched == null)
                {
                    Plugin.Instance.Logger.Warn("DefaultTmdbLibrary - library not found: " + libraryName);
                    return;
                }

                var options = matched.LibraryOptions;
                if (options == null) return;

                var changed = false;

                // Emby 4.9: 刮削器按内容类型配置在 TypeOptions 里
                foreach (var typeOptions in options.TypeOptions)
                {
                    if (typeOptions?.MetadataFetchers is { Length: > 0 })
                    {
                        typeOptions.MetadataFetchers =
                            MoveTheMovieDbFirst(typeOptions.MetadataFetchers, ref changed);
                    }

                    if (typeOptions?.ImageFetchers is { Length: > 0 })
                    {
                        typeOptions.ImageFetchers =
                            MoveTheMovieDbFirst(typeOptions.ImageFetchers, ref changed);
                    }
                }

                if (changed && long.TryParse(matched.ItemId, out var itemId))
                {
                    CollectionFolder.SaveLibraryOptions(itemId, options);
                    Plugin.Instance.Logger.Info(
                        $"DefaultTmdbLibrary - TheMovieDb set as primary fetcher for library '{libraryName}'");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("DefaultTmdbLibrary - apply failed: " + ex.Message);
            }
        }

        private static string[] MoveTheMovieDbFirst(string[] fetchers, ref bool changed)
        {
            if (fetchers == null || fetchers.Length < 2) return fetchers;

            if (string.Equals(fetchers[0], "TheMovieDb", StringComparison.OrdinalIgnoreCase)) return fetchers;

            var index = Array.FindIndex(fetchers, f =>
                string.Equals(f, "TheMovieDb", StringComparison.OrdinalIgnoreCase));
            if (index <= 0) return fetchers;

            changed = true;
            var reordered = new string[fetchers.Length];
            reordered[0] = fetchers[index];
            var offset = 1;
            for (var i = 0; i < fetchers.Length; i++)
            {
                if (i == index) continue;
                reordered[offset++] = fetchers[i];
            }

            return reordered;
        }
    }
}
