using MediaBrowser.Common.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 本地 TMDB 语义化缓存（v2）：把 MovieDb 的元数据请求按 TMDB 实体（电影/剧集/季/集/合集）
    /// 映射到 PRO 同构的目录布局，实现「override 优先 + 被动缓存」。
    ///
    /// 目录布局（与 PRO 同名，可直接复用 PRO 的 override 资源）：
    ///   &lt;root&gt;\override\tmdb-movies2\&lt;tmdbid&gt;\all.json          （覆盖缓存，恒 P1，用户自定义）
    ///   &lt;root&gt;\override\tmdb-tv\&lt;tmdbid&gt;\series.json
    ///   &lt;root&gt;\override\tmdb-tv\&lt;tmdbid&gt;\season-&lt;n&gt;.json
    ///   &lt;root&gt;\override\tmdb-tv\&lt;tmdbid&gt;\season-&lt;s&gt;-episode-&lt;e&gt;.json
    ///   &lt;root&gt;\override\tmdb-collections\&lt;tmdbid&gt;\all.json
    ///   &lt;root&gt;\cache\&lt;同上&gt;                                      （被动缓存，读写）
    ///   &lt;root&gt;\patches\&lt;同上&gt;                                    （合并补丁，v3 使用）
    ///
    /// URL 映射（MovieDb 元数据出口的 URL 特征）：
    ///   /3/movie/{id}                        -> tmdb-movies2 / all.json
    ///   /3/tv/{id}                           -> tmdb-tv / series.json
    ///   /3/tv/{id}/season/{n}                -> tmdb-tv / season-{n}.json
    ///   /3/tv/{id}/season/{s}/episode/{e}    -> tmdb-tv / season-{s}-episode-{e}.json
    ///   /3/collection/{id}                   -> tmdb-collections / all.json
    ///
    /// 注意：PRO 的缓存是「备选语言刮削处理后的综合结果」，本实现按实体落盘、不区分 language，
    /// 与用户固定的备选语言顺序一致；若需多语言并存，需在 v3 引入语言后缀。
    /// </summary>
    public static class LocalMetadataCache
    {
        private static readonly object _lock = new object();
        private static string _root;

        private static readonly Regex EntityUrl = new Regex(
            @"^https?://[^/]+/3/(movie|tv|collection)/(\d+)(?:/season/(\d+))?(?:/episode/(\d+))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Root => _root;

        public sealed class CacheEntry
        {
            public string TypeDir { get; set; }
            public string TmdbId { get; set; }
            public string FileName { get; set; }

            public string RelativePath => Path.Combine(TypeDir, TmdbId, FileName);
        }

        /// <summary>
        /// 校验并初始化缓存根目录，创建所需子目录。返回是否可用及原因。
        /// </summary>
        public static (bool ok, string error) ValidateAndInitialize(string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root)) return (false, "EMPTY");

                var full = Path.GetFullPath(root.Trim());

                // 拒绝明显危险/无效的根：盘符根、系统目录
                if (string.Equals(full.TrimEnd('\\', '/'), Path.GetPathRoot(full)?.TrimEnd('\\', '/'),
                        StringComparison.OrdinalIgnoreCase))
                    return (false, "ROOT_NOT_ALLOWED");

                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(winDir) &&
                    full.StartsWith(winDir, StringComparison.OrdinalIgnoreCase))
                    return (false, "ROOT_NOT_ALLOWED");

                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);

                foreach (var scope in new[] { "cache", "override", "patches" })
                {
                    foreach (var type in new[] { "tmdb-movies2", "tmdb-tv", "tmdb-collections" })
                    {
                        Directory.CreateDirectory(Path.Combine(full, scope, type));
                    }
                }

                lock (_lock) { _root = full; }
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        public static void Reset()
        {
            lock (_lock) { _root = null; }
        }

        /// <summary>
        /// 把 MovieDb 元数据请求 URL 解析为 TMDB 实体条目；非实体请求（人物/搜索/配置等）返回 false。
        /// </summary>
        public static bool TryParseEntity(string url, out CacheEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(url)) return false;

            try
            {
                var match = EntityUrl.Match(url);
                if (!match.Success) return false;

                var kind = match.Groups[1].Value.ToLowerInvariant();
                var id = match.Groups[2].Value;
                var season = match.Groups[3].Success ? match.Groups[3].Value : null;
                var episode = match.Groups[4].Success ? match.Groups[4].Value : null;

                string typeDir, fileName;
                switch (kind)
                {
                    case "movie":
                        typeDir = "tmdb-movies2";
                        fileName = "all.json";
                        break;
                    case "collection":
                        typeDir = "tmdb-collections";
                        fileName = "all.json";
                        break;
                    default: // tv
                        typeDir = "tmdb-tv";
                        if (episode != null && season != null)
                            fileName = $"season-{season}-episode-{episode}.json";
                        else if (season != null)
                            fileName = $"season-{season}.json";
                        else
                            fileName = "series.json";
                        break;
                }

                entry = new CacheEntry { TypeDir = typeDir, TmdbId = id, FileName = fileName };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildPath(string scope, CacheEntry entry)
        {
            var root = _root;
            if (string.IsNullOrEmpty(root) || entry == null) return null;
            return Path.Combine(root, scope, entry.TypeDir, entry.TmdbId, entry.FileName);
        }

        public static string GetOverrideFilePath(CacheEntry entry) => BuildPath("override", entry);
        public static string GetPassiveFilePath(CacheEntry entry) => BuildPath("cache", entry);
        public static string GetPatchFilePath(CacheEntry entry) => BuildPath("patches", entry);

        private static bool TryRead(string path, out byte[] bytes)
        {
            bytes = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                bytes = File.ReadAllBytes(path);
                return bytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadOverride(CacheEntry entry, out byte[] bytes) =>
            TryRead(GetOverrideFilePath(entry), out bytes);

        public static bool TryReadPassive(CacheEntry entry, out byte[] bytes) =>
            TryRead(GetPassiveFilePath(entry), out bytes);

        public static void WritePassive(CacheEntry entry, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;

            try
            {
                var path = GetPassiveFilePath(entry);
                if (string.IsNullOrEmpty(path)) return;

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var tmp = path + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch
            {
                // 缓存写入失败不影响主流程
            }
        }

        public static byte[] ReadAll(Stream stream)
        {
            if (stream == null) return null;

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// 合并补丁：若存在 <c>patches/&lt;type&gt;/&lt;tmdbid&gt;/&lt;file&gt;.json</c>，
        /// 则把其顶层字段合并到 base 之上（部分信息覆盖，如标题/简介）。无补丁或失败时原样返回。
        /// 顶层字段合并为幂等操作，故「读缓存后再合并」不会产生漂移。
        /// </summary>
        public static byte[] ApplyPatches(CacheEntry entry, byte[] baseBytes)
        {
            if (entry == null || baseBytes == null || baseBytes.Length == 0) return baseBytes;

            var patchPath = GetPatchFilePath(entry);

            try
            {
                if (string.IsNullOrEmpty(patchPath) || !File.Exists(patchPath)) return baseBytes;

                var serializer = Plugin.JsonSerializer;
                if (serializer == null) return baseBytes;

                var patchText = File.ReadAllText(patchPath);
                if (string.IsNullOrWhiteSpace(patchText)) return baseBytes;

                var baseObj = serializer.DeserializeFromString<Dictionary<string, object>>(
                    Encoding.UTF8.GetString(baseBytes));
                var patchObj = serializer.DeserializeFromString<Dictionary<string, object>>(patchText);

                if (baseObj == null ||
                    patchObj == null) return baseBytes;

                foreach (var kv in patchObj)
                {
                    baseObj[kv.Key] = kv.Value;
                }

                var merged = serializer.SerializeToString(baseObj);

                if (Plugin.Instance.DebugMode)
                {
                    Plugin.Instance.Logger.Debug(
                        $"LocalTmdbCache - PATCH applied ({patchObj.Count} field(s)) {entry.RelativePath}");
                }

                return Encoding.UTF8.GetBytes(merged);
            }
            catch (Exception e)
            {
                Plugin.Instance.Logger.Debug("LocalTmdbCache patch merge failed: " + e.Message);
                return baseBytes;
            }
        }

        /// <summary>
        /// 用缓存的字节构造一个可供 MovieDb 消费的 HttpResponseInfo。
        /// </summary>
        public static HttpResponseInfo BuildResponse(byte[] bytes, string url)
        {
            return new HttpResponseInfo
            {
                Content = new MemoryStream(bytes, false),
                StatusCode = HttpStatusCode.OK,
                ContentType = "application/json",
                ContentLength = bytes?.LongLength ?? 0,
                ResponseUrl = url
            };
        }
    }
}
