using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

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
    /// </summary>
    public static class IsoMounter
    {
        private const string MountCmdlet = "Mount-DiskImage";
        private const string DismountCmdlet = "Dismount-DiskImage";

        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
        private static readonly object _lock = new object();

        private sealed class MountEntry
        {
            public string DriveRoot;
            public DateTime LastUsedUtc;
            public bool Failed;
        }

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
