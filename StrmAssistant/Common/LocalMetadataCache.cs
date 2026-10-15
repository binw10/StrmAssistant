using MediaBrowser.Common.Net;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 本地 TMDB 被动缓存（v1）：把 MovieDb 元数据 API 的原始响应按 URL 落地到磁盘，
    /// 用于限流/断网时复用，并为后续语义化 override/patches（PRO 同构）预留目录结构。
    ///
    /// 目录布局（与 PRO 保持同名，便于将来演进）：
    ///   &lt;root&gt;\cache\tmdb-movies2 | tmdb-tv | tmdb-collections   （被动缓存，语义化：v2 使用）
    ///   &lt;root&gt;\cache\_http                                         （v1 原始响应，按 URL 的 SHA256 命名）
    ///   &lt;root&gt;\override\tmdb-movies2 | tmdb-tv | tmdb-collections （覆盖缓存：v2 使用）
    /// </summary>
    public static class LocalMetadataCache
    {
        private static readonly object _lock = new object();
        private static string _root;

        public static string Root => _root;

        public static string RawResponseDir =>
            string.IsNullOrEmpty(_root) ? null : Path.Combine(_root, "cache", "_http");

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

                foreach (var type in new[] { "tmdb-movies2", "tmdb-tv", "tmdb-collections" })
                {
                    Directory.CreateDirectory(Path.Combine(full, "cache", type));
                    Directory.CreateDirectory(Path.Combine(full, "override", type));
                }

                Directory.CreateDirectory(Path.Combine(full, "cache", "_http"));

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

        private static string CachePath(string url)
        {
            var dir = RawResponseDir;
            if (string.IsNullOrEmpty(dir)) return null;

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
            var name = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            return Path.Combine(dir, name + ".bin");
        }

        public static bool TryGet(string url, out byte[] bytes)
        {
            bytes = null;
            try
            {
                var path = CachePath(url);
                if (path == null || !File.Exists(path)) return false;

                bytes = File.ReadAllBytes(path);
                return bytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static void Set(string url, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;

            try
            {
                var path = CachePath(url);
                if (path == null) return;

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
