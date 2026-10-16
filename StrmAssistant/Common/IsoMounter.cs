using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace StrmAssistant.Common
{
    /// <summary>
    /// ISO 正片定位器（跨平台，免 OS 强依赖）。
    ///
    /// - **Windows**：调用 PowerShell 的 <c>Mount-DiskImage</c> 把 .iso 挂成虚拟光驱，
    ///   再由 <see cref="UdfTitleResolver.ResolveFromMount"/> 在挂载卷上按 playlist 选出正片
    ///   （失败回退「最大 .m2ts / VTS_01_1.VOB」），交给 ffprobe 探测。
    /// - **Linux / Docker**：无法挂载，改由 <see cref="UdfTitleResolver.Resolve"/> 进程内读 UDF
    ///   选出正片（仅用于发现与日志校验），.iso 本身仍交由 <c>CustomProbePath</c> 指定的
    ///   带 ISO 支持的自定义 ffprobe 直读。
    ///
    /// 生命周期：按 ISO 路径缓存挂载结果（含最后使用时间），后台清扫器在空闲超过
    /// <see cref="IdleTimeout"/> 后自动卸载；插件卸载时全部卸载。
    ///
    /// **远程 ISO（strm/http）**：无法挂载，且 Windows 版 libbluray 无法直读 UDF（实测
    /// <c>bluray:</c> 协议在文件路径/挂载盘/HTTP 上均失败）。改为：
    /// <see cref="GetRemoteProbePath"/> 用 HTTP Range 拉取 ISO **头部窗口**（默认 8MB）
    /// 落到本地临时文件，再由 ffprobe 探测该临时文件——实测可正确得到正片流信息。
    /// 临时文件按 URL 缓存并有空闲清理。
    /// </summary>
    public static class IsoMounter
    {
        private const string MountCmdlet = "Mount-DiskImage";
        private const string DismountCmdlet = "Dismount-DiskImage";

        /// <summary>远程 ISO 下载窗口大小：足够覆盖 UDF 头部 + 首个 m2ts 的 SPS/VPS（默认 8MB）。</summary>
        private const long RemoteWindowBytes = 8L * 1024 * 1024;

        /// <summary>远程 ISO 下载超时（毫秒）。慢速代理下 8MB 可能需要 1~2 分钟。</summary>
        private const int RemoteDownloadTimeoutMs = 180_000;

        /// <summary>读取空闲看门狗：连续该时长未收到新数据即中止（毫秒）。</summary>
        private const int ReadIdleTimeoutMs = 20_000;

        /// <summary>可接受的最小字节数：达到即视为窗口可用（典型 ISO 头部窗口远大于此）。</summary>
        private const long MinAcceptableBytes = 1L * 1024 * 1024;

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan RemoteCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly object _lock = new object();

        private sealed class MountEntry
        {
            public string DriveRoot;
            public DateTime LastUsedUtc;
            public bool Failed;
        }

        private sealed class RemoteEntry
        {
            public string TempFile;
            public DateTime LastUsedUtc;
        }

        private static readonly ConcurrentDictionary<string, RemoteEntry> RemoteIsos =
            new ConcurrentDictionary<string, RemoteEntry>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, MountEntry> Mounts =
            new ConcurrentDictionary<string, MountEntry>(StringComparer.OrdinalIgnoreCase);

        private static Timer _sweeper;
        private static bool? _supported;

        /// <summary>当前平台是否支持「OS 原生挂载」（Windows + 存在 Mount-DiskImage）。</summary>
        public static bool IsSupported
        {
            get
            {
                if (_supported.HasValue) return _supported.Value;

                try
                {
                    _supported = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                 !string.IsNullOrEmpty(RunPowerShell(
                                     $"if (Get-Command {MountCmdlet} -ErrorAction SilentlyContinue) {{ 'yes' }}"));
                }
                catch
                {
                    _supported = false;
                }

                return _supported.Value;
            }
        }

        /// <summary>是否具备进程内 UDF 读盘能力（本插件内建 DiscUtils，跨平台恒可用）。</summary>
        public static bool UdfReadSupported => true;

        /// <summary>
        /// 宽松判断：路径是否「看起来是 ISO」——用于解锁 Emby 对 .iso 的探测闸门。
        ///
        /// 与 <see cref="TryNormalizeIsoPath"/> / <see cref="TryNormalizeRemoteIso"/> 不同，
        /// 这里**不做**存在性/协议校验，只按后缀判断；因为闸门处的 path 可能是：
        /// - 本地裸路径 <c>D:\a.iso</c>
        /// - ffmpeg 包裹形式 <c>file:"D:\a.iso"</c>
        /// - 远程 URL <c>http://x/api/strm?...&amp;file_name=a.iso</c>（含查询串，Path.GetExtension 仍为 .iso）
        ///
        /// 注意：URL 场景下 <see cref="Path.GetExtension(string)"/> 会因查询串返回空，故需先剥离 query。
        /// </summary>
        public static bool LooksLikeIsoPath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var candidate = raw.Trim().Trim('"', '\'').Trim();
            if (candidate.Length == 0) return false;

            // 去掉 file: 前缀
            if (candidate.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(5).Trim().Trim('"', '\'').Trim();

            // ① 整体以 .iso 结尾（含 strm 代理 URL 把文件名放在 query 末尾的情形）
            if (candidate.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return true;

            // ② 剥离 query/fragment 后按路径扩展名判断；并额外对 query 做 URL 解码再判一次
            var q = candidate.IndexOf('?');
            if (q >= 0)
            {
                var pathPart = candidate.Substring(0, q);
                if (pathPart.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return true;

                try
                {
                    var decoded = Uri.UnescapeDataString(candidate);
                    if (decoded.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return true;

                    var dq = decoded.IndexOf('?');
                    if (dq >= 0 && decoded.Substring(0, dq).EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // 解码失败忽略
                }
            }

            return false;
        }

        /// <summary>
        /// 判断是否为本地 ISO 文件路径。
        /// 兼容 ffmpeg 输入串的两种包裹：<c>file:"D:\a.iso"</c>（本地文件）与 <c>"D:\a.iso"</c>。
        /// </summary>
        public static bool TryNormalizeIsoPath(string raw, out string isoPath)
        {
            isoPath = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var candidate = raw.Trim();

            // 去掉 Emby 为本地文件生成的 file: 协议前缀
            if (candidate.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(5).Trim();

            candidate = candidate.Trim('"', '\'').Trim();
            if (candidate.Length == 0) return false;

            // 排除 concat/多输入与远程协议
            if (candidate.Contains("://") || candidate.Contains('|') ||
                candidate.Contains("concat:", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!candidate.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                if (!File.Exists(candidate)) return false;
            }
            catch
            {
                return false;
            }

            isoPath = candidate;
            return true;
        }

        /// <summary>
        /// 判断是否为「远程/流式」ISO 输入（http/https/strm 代理的 ISO）。
        ///
        /// 与本地的区别：
        /// - **不做** <see cref="File.Exists"/> 校验（URL 无法落地）；
        /// - 允许 `://`（http/https），但**拒绝** concat/多输入/管道串。
        /// - 兼容 ffprobe 已包裹的形式，如 <c>"http://x/a.iso"</c> 或 <c>file:"..."</c> 之外的裸 URL。
        ///
        /// 命中后由 <see cref="GetRemoteProbePath"/> 拉取头部窗口到临时文件再探测。
        /// </summary>
        public static bool TryNormalizeRemoteIso(string raw, out string isoUrl)
        {
            isoUrl = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var candidate = raw.Trim().Trim('"', '\'').Trim();
            if (candidate.Length == 0) return false;

            // 排除 concat/多输入/管道（远程 ISO 不应出现在这些复合串里）
            if (candidate.Contains('|') ||
                candidate.Contains("concat:", StringComparison.OrdinalIgnoreCase))
                return false;

            // 必须是 http/https 绝对 URL（strm 代理场景；其余协议如 smb 暂无内置支持，交由自定义 ffprobe）
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

            // 以「路径部分」（剥离 query/fragment）判断是否 .iso，避免查询串影响
            if (!uri.AbsolutePath.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) &&
                !candidate.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
                return false;

            isoUrl = candidate;
            return true;
        }

        /// <summary>
        /// 远程 ISO → 本地临时文件（可交给 ffprobe 探测）。
        ///
        /// 做法：用 HTTP Range 拉取 ISO 的**头部窗口**（<see cref="RemoteWindowBytes"/>），
        /// 落盘到临时目录并缓存（按 URL，TTL <see cref="RemoteCacheTtl"/>）。
        /// 实测该窗口足以让 ffprobe 正确识别正片流（4K BL + 1080p EL）。
        ///
        /// 失败返回 null（调用方保持原路径放行）。
        /// </summary>
        public static string GetRemoteProbePath(string isoUrl)
        {
            if (string.IsNullOrEmpty(isoUrl)) return null;

            try
            {
                EnsureSweeper();

                if (RemoteIsos.TryGetValue(isoUrl, out var cached) &&
                    File.Exists(cached.TempFile))
                {
                    cached.LastUsedUtc = DateTime.UtcNow;
                    Plugin.Instance?.Logger?.Info($"IsoExtraction - using cached remote iso window {cached.TempFile}");
                    return cached.TempFile;
                }

                var tempDir = Path.Combine(Path.GetTempPath(), "strmassistant_iso");
                Directory.CreateDirectory(tempDir);

                var fileName = "iso_" + Math.Abs(isoUrl.GetHashCode()).ToString("x8") + "_" +
                               Guid.NewGuid().ToString("N").Substring(0, 8) + ".iso";
                var tempFile = Path.Combine(tempDir, fileName);

                Plugin.Instance?.Logger?.Info(
                    $"IsoExtraction - downloading remote iso window ({RemoteWindowBytes / (1024 * 1024)}MB) for {SafeShorten(isoUrl)}");

                if (!DownloadRange(isoUrl, 0, RemoteWindowBytes - 1, tempFile))
                {
                    TryDelete(tempFile);
                    return null;
                }

                var len = new FileInfo(tempFile).Length;
                Plugin.Instance?.Logger?.Info($"IsoExtraction - remote iso window saved: {tempFile} ({len} bytes)");

                RemoteIsos[isoUrl] = new RemoteEntry { TempFile = tempFile, LastUsedUtc = DateTime.UtcNow };
                return tempFile;
            }
            catch (Exception e)
            {
                Plugin.Instance?.Logger?.Warn("IsoExtraction - remote iso download failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// HTTP Range 下载字节区间到本地文件。
        ///
        /// 与常规实现的差异（应对 strm 代理限流/卡死）：
        /// - **读取空闲看门狗**：连续 <see cref="ReadIdleTimeoutMs"/> 未收到新数据即中止，
        ///   返回已下载的部分内容（避免 HttpClient.Timeout 在某些代理上完全不触发而永久挂起）。
        /// - **接受部分内容**：只要已拿到 ≥ <see cref="MinAcceptableBytes"/>，即视为成功；
        ///   ISO 头部窗口（UDF 结构 + 首个 mpegts 同步头）在数 MB 内即可被 ffprobe 识别。
        /// - 目标文件以 <see cref="FileShare.Read"/> 打开，保持期间外部只读也能访问。
        ///
        /// 成功（含部分）返回 true。
        /// </summary>
        private static bool DownloadRange(string url, long from, long to, string destFile)
        {
            var total = 0L;
            try
            {
                var handler = new System.Net.Http.HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = System.Net.DecompressionMethods.None
                };
                using var client = new System.Net.Http.HttpClient(handler)
                {
                    Timeout = Timeout.InfiniteTimeSpan // 交由下方看门狗控制
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Emby/4.10 StrmAssistant");

                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);

                using var response = client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    Plugin.Instance?.Logger?.Warn(
                        $"IsoExtraction - range request returned {(int)response.StatusCode}");
                    return false;
                }

                using var src = response.Content.ReadAsStream();
                using var dst = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 20);

                var buffer = new byte[1 << 20];
                var want = to - from + 1;
                var deadlineUtc = DateTime.UtcNow.AddMilliseconds(RemoteDownloadTimeoutMs);

                while (total < want)
                {
                    if (DateTime.UtcNow >= deadlineUtc)
                    {
                        Plugin.Instance?.Logger?.Warn(
                            $"IsoExtraction - range download reached overall timeout ({RemoteDownloadTimeoutMs}ms), " +
                            $"aborting with {total} bytes");
                        break;
                    }

                    // 异步读 + 空闲看门狗：任一先到即继续；空闲超时视为流已停滞
                    var readTask = src.ReadAsync(buffer, 0, buffer.Length);
                    var completed = Task.WhenAny(readTask, Task.Delay(ReadIdleTimeoutMs)).GetAwaiter().GetResult();

                    if (completed != readTask)
                    {
                        Plugin.Instance?.Logger?.Warn(
                            $"IsoExtraction - range download idle {ReadIdleTimeoutMs}ms, aborting with {total} bytes");
                        break;
                    }

                    var read = readTask.GetAwaiter().GetResult();
                    if (read <= 0) break;

                    dst.Write(buffer, 0, read);
                    total += read;
                }

                dst.Flush();
            }
            catch (Exception e)
            {
                Plugin.Instance?.Logger?.Warn("IsoExtraction - range download error: " + e.Message);
            }

            if (total >= MinAcceptableBytes) return true;

            Plugin.Instance?.Logger?.Warn(
                $"IsoExtraction - range download produced only {total} bytes (< {MinAcceptableBytes}); discarding");
            return false;
        }

        private static string SafeShorten(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            var q = url.IndexOf('?');
            return q > 0 ? url.Substring(0, q) + "?...(len=" + url.Length + ")" : url;
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        /// <summary>
        /// 返回可用于 ffprobe 的「正片」路径；无可用真实路径时返回 null（调用方保持原路径）。
        ///
        /// Windows 挂载成功 → 返回挂载卷上的正片路径；
        /// 非 Windows 或挂载失败 → 返回 null，仅做一次进程内 UDF 选片记录（Linux/Docker 由自定义 ffprobe 直读 .iso）。
        /// </summary>
        public static string GetProbePath(string isoPath)
        {
            if (string.IsNullOrEmpty(isoPath)) return null;

            if (IsSupported)
            {
                try
                {
                    var entry = Mounts.GetOrAdd(isoPath, k => Mount(k));
                    entry.LastUsedUtc = DateTime.UtcNow;

                    if (!entry.Failed && !string.IsNullOrEmpty(entry.DriveRoot))
                    {
                        var title = ResolveMountedTitle(isoPath, entry.DriveRoot);
                        if (!string.IsNullOrEmpty(title)) return title;

                        Plugin.Instance?.Logger?.Info(
                            $"IsoExtraction - no main title found under {entry.DriveRoot} for {isoPath}");
                    }
                }
                catch (Exception e)
                {
                    Plugin.Instance?.Logger?.Warn("IsoExtraction - mount failed: " + e.Message);
                }
            }

            ResolveInProcessTitle(isoPath);
            return null;
        }

        /// <summary>
        /// 已挂载卷上定位正片：优先按 playlist 选片（多 m2ts / 无缝分支才不会选错），再回退旧逻辑。
        /// </summary>
        private static string ResolveMountedTitle(string isoPath, string driveRoot)
        {
            var info = UdfTitleResolver.ResolveFromMount(driveRoot);

            if (!string.IsNullOrEmpty(info?.RelativePath))
            {
                var path = Path.Combine(driveRoot, info.RelativePath);

                if (File.Exists(path))
                {
                    Plugin.Instance?.Logger?.Info(
                        $"IsoExtraction - selected {info.RelativePath} " +
                        $"(playlist {info.SourceName}, {info.DurationSeconds:F1}s) from {isoPath}");
                    return path;
                }

                Plugin.Instance?.Logger?.Warn(
                    $"IsoExtraction - selected title {info.RelativePath} not found on {driveRoot} for {isoPath}");
            }

            var fallback = ResolveMainTitle(driveRoot);
            if (!string.IsNullOrEmpty(fallback))
            {
                Plugin.Instance?.Logger?.Info(
                    $"IsoExtraction - fallback to largest title {fallback} for {isoPath}");
            }

            return fallback;
        }

        /// <summary>
        /// 非挂载路径：进程内读 UDF 选出正片，仅记录结果用于发现与日志校验（不改探测输入）。
        /// </summary>
        private static void ResolveInProcessTitle(string isoPath)
        {
            if (!UdfReadSupported) return;

            var info = UdfTitleResolver.Resolve(isoPath);
            var logger = Plugin.Instance?.Logger;
            if (logger == null) return;

            if (info == null)
            {
                logger.Debug($"IsoExtraction - UDF title resolution found nothing in {isoPath}");
                return;
            }

            logger.Info(
                $"IsoExtraction - UDF resolved {(info.IsBluray ? "bluray" : "dvd")} title {info.RelativePath} " +
                $"(playlist {info.SourceName}, {info.DurationSeconds:F1}s) in {isoPath}; probing the iso directly");
        }

        private static MountEntry Mount(string isoPath)
        {
            var entry = new MountEntry { LastUsedUtc = DateTime.UtcNow };

            try
            {
                EnsureSweeper();

                var script =
                    $"$ErrorActionPreference='Stop'; " +
                    $"$img = Mount-DiskImage -ImagePath '{EscapePs(isoPath)}' -PassThru; " +
                    "($img | Get-Volume).DriveLetter";

                var output = RunPowerShell(script)?.Trim();

                if (string.IsNullOrEmpty(output) || output.Length > 3)
                {
                    entry.Failed = true;
                    Plugin.Instance?.Logger?.Warn(
                        $"IsoExtraction - Mount-DiskImage returned unexpected output '{output}' for {isoPath}");
                    return entry;
                }

                entry.DriveRoot = output.TrimEnd(':') + ":\\";
                Plugin.Instance?.Logger?.Info($"IsoExtraction - mounted {isoPath} -> {entry.DriveRoot}");
            }
            catch (Exception e)
            {
                entry.Failed = true;
                Plugin.Instance?.Logger?.Warn($"IsoExtraction - Mount-DiskImage failed for {isoPath}: {e.Message}");
            }

            return entry;
        }

        /// <summary>
        /// 在已挂载的卷里定位正片：
        /// 1) Bluray: BDMV\STREAM 下最大的 .m2ts
        /// 2) DVD:    VIDEO_TS\VTS_01_1.VOB（缺失则取最大的 .VOB）
        /// 3) 兜底:   卷内（深度 ≤ 3）最大的常见视频文件
        /// </summary>
        private static string ResolveMainTitle(string driveRoot)
        {
            try
            {
                var bdmvStream = Path.Combine(driveRoot, "BDMV", "STREAM");
                var m2ts = PickLargest(bdmvStream, "*.m2ts", "*.M2TS");
                if (!string.IsNullOrEmpty(m2ts)) return m2ts;

                var videoTs = Path.Combine(driveRoot, "VIDEO_TS");
                if (Directory.Exists(videoTs))
                {
                    var main = Path.Combine(videoTs, "VTS_01_1.VOB");
                    if (File.Exists(main)) return main;

                    var vob = PickLargest(videoTs, "*.VOB", "*.vob");
                    if (!string.IsNullOrEmpty(vob)) return vob;
                }

                // 兜底：递归（限深）找最大视频文件
                string[] extensions = { ".m2ts", ".mts", ".mkv", ".mp4", ".ts", ".vob", ".avi", ".mov", ".mpg", ".mpeg", ".wmv", ".flv" };
                var best = Directory.EnumerateFiles(driveRoot, "*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.Length)
                    .FirstOrDefault();

                return best?.FullName;
            }
            catch (Exception e)
            {
                Plugin.Instance?.Logger?.Debug("IsoExtraction - ResolveMainTitle failed: " + e.Message);
                return null;
            }
        }

        private static string PickLargest(string dir, params string[] patterns)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;

                return patterns
                    .SelectMany(p => Directory.EnumerateFiles(dir, p))
                    .Select(f => new FileInfo(f))
                    .Where(f => f.Length > 0)
                    .OrderByDescending(f => f.Length)
                    .Select(f => f.FullName)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureSweeper()
        {
            lock (_lock)
            {
                _sweeper ??= new Timer(_ => SweepIdle(), null, IdleTimeout, TimeSpan.FromMinutes(1));
            }
        }

        private static void SweepIdle()
        {
            try
            {
                foreach (var kv in Mounts.ToArray())
                {
                    if (kv.Value.Failed) { Mounts.TryRemove(kv.Key, out _); continue; }
                    if (DateTime.UtcNow - kv.Value.LastUsedUtc < IdleTimeout) continue;

                    if (Dismount(kv.Key)) Mounts.TryRemove(kv.Key, out _);
                }

                // 远程 ISO 临时文件：超过 TTL 清理
                foreach (var kv in RemoteIsos.ToArray())
                {
                    if (DateTime.UtcNow - kv.Value.LastUsedUtc < RemoteCacheTtl) continue;

                    TryDelete(kv.Value.TempFile);
                    RemoteIsos.TryRemove(kv.Key, out _);
                }
            }
            catch
            {
                // 忽略
            }
        }

        public static void DismountAll()
        {
            try
            {
                _sweeper?.Dispose();
                _sweeper = null;
            }
            catch
            {
                // 忽略
            }

            foreach (var kv in Mounts.ToArray())
            {
                if (Dismount(kv.Key)) Mounts.TryRemove(kv.Key, out _);
            }

            // 清理全部远程 ISO 临时文件
            foreach (var kv in RemoteIsos.ToArray())
            {
                TryDelete(kv.Value.TempFile);
                RemoteIsos.TryRemove(kv.Key, out _);
            }
        }

        private static bool Dismount(string isoPath)
        {
            try
            {
                RunPowerShell(
                    $"$ErrorActionPreference='SilentlyContinue'; Dismount-DiskImage -ImagePath '{EscapePs(isoPath)}'");

                Plugin.Instance?.Logger?.Info($"IsoExtraction - dismounted {isoPath}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Instance?.Logger?.Debug($"IsoExtraction - dismount failed for {isoPath}: {e.Message}");
                return false;
            }
        }

        private static string EscapePs(string value) => value.Replace("'", "''");

        private static string RunPowerShell(string script)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                            script.Replace("\"", "\\\"") + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var stdout = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(30000))
            {
                try { process.Kill(true); } catch { /* 忽略 */ }
                return null;
            }

            return stdout;
        }
    }
}
