using HarmonyLib;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static StrmAssistant.Common.LanguageUtility;
using static StrmAssistant.Mod.PatchManager;

namespace StrmAssistant.Mod
{
    public class PinyinSortName : PatchBase<PinyinSortName>
    {
        private static MethodInfo _createSortName;
        private static MethodInfo _getPrefixes;
        private static MethodInfo _getArtistPrefixes;

        public PinyinSortName()
        {
            Initialize();

            if (Plugin.Instance.MetadataEnhanceStore.GetOptions().PosterSort.PinyinSortName)
            {
                Patch();
            }
        }

        protected override void OnInitialize()
        {
            _createSortName = typeof(BaseItem).GetMethod("CreateSortName",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(ReadOnlySpan<char>) }, null);
            var embyApi = Assembly.Load("Emby.Api");
            var tagService = embyApi.GetType("Emby.Api.UserLibrary.TagService");
            _getPrefixes =
                tagService.GetMethod("Get", new[] { embyApi.GetType("Emby.Api.UserLibrary.GetPrefixes") });
            _getArtistPrefixes =
                tagService.GetMethod("Get", new[] { embyApi.GetType("Emby.Api.UserLibrary.GetArtistPrefixes") });
        }

        protected override void Prepare(bool apply)
        {
            PatchUnpatch(PatchTracker, apply, _createSortName, postfix: nameof(CreateSortNamePostfix));
            PatchUnpatch(PatchTracker, apply, _getPrefixes, postfix: nameof(GetPrefixesPostfix));
            PatchUnpatch(PatchTracker, apply, _getArtistPrefixes, postfix: nameof(GetPrefixesPostfix));
        }

        [HarmonyPostfix]
        private static void CreateSortNamePostfix(BaseItem __instance, ref ReadOnlySpan<char> __result)
        {
            if (__instance.SupportsUserData && __instance.EnableAlphaNumericSorting && !(__instance is IHasSeries) &&
                (__instance is Video || __instance is Audio || __instance is IItemByName ||
                 __instance is Folder) && !__instance.IsFieldLocked(MetadataFields.SortName))
            {
                var result = new string(__result);

                if (IsChinese(result))
                {
                    var nameToProcess = __instance is BoxSet ? RemoveDefaultCollectionName(result) : result;

                    result = ConvertToPinyinInitials(nameToProcess);
                }

                // OptimizeMediaItemOrder：SortName 数字段左补零实现自然排序（2 排在 10 前）。
                // SortName 仅用于排序不用于显示，补零无视觉影响。
                if (Plugin.Instance.ExperienceEnhanceStore.GetOptions().LibraryOrder.OptimizeMediaItemOrder &&
                    ContainsDigits(result))
                {
                    result = Naturalize(result);
                }

                __result = result.AsSpan();
            }
        }

        private static bool ContainsDigits(string value)
        {
            foreach (var ch in value)
            {
                if (ch >= '0' && ch <= '9') return true;
            }

            return false;
        }

        private static readonly System.Text.RegularExpressions.Regex DigitSequence =
            new System.Text.RegularExpressions.Regex(@"\d+",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string Naturalize(string sortName)
        {
            return DigitSequence.Replace(sortName, m => m.Value.PadLeft(10, '0'));
        }

        [HarmonyPostfix]
        private static void GetPrefixesPostfix(object request, ref object __result)
        {
            if (__result is NameValuePair[] pairs)
            {
                var validChars = new HashSet<char>("#ABCDEFGHIJKLMNOPQRSTUVWXYZ");

                var filteredPairs = pairs.Where(p => p.Name?.Length == 1 && validChars.Contains(p.Name[0])).ToArray();

                if (filteredPairs.Length != pairs.Length && filteredPairs.Any(p => p.Name[0] != '#'))
                {
                    __result = filteredPairs;
                }
            }
        }
    }
}
