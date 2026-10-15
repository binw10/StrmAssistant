using DiscUtils.Udf;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StrmAssistant.Common
{
    /// <summary>
    /// 碟片正片解析（跨平台，进程内读 UDF，不依赖任何 OS 挂载能力）。
    ///
    /// 蓝光：遍历 <c>BDMV\PLAYLIST\*.mpls</c>，按 PlayItem 时长求和取「总时长最长的 playlist」，
    /// 再取其中时长最长的 PlayItem 的 5 位号 → <c>BDMV\STREAM\&lt;5 位号&gt;.m2ts</c>
    /// （MPLS 的 Clip_Information_file_name 与 STREAM 内 m2ts 同名，符合 BD 规范）。
    /// 按 playlist 求和选片，多 m2ts 分段 / 无缝分支碟才不会选错；MPLS 时间戳为 45kHz。
    /// DVD：<c>VIDEO_TS\VTS_01_1.VOB</c>，缺失则取最大 .VOB。
    ///
    /// 两种来源共用同一套选片逻辑：<see cref="IDiscFs"/> 分别由进程内 UDF 与已挂载卷实现。
    /// 只读、静默失败：任何异常都返回 null，调用方保持原路径不变。
    /// </summary>
    internal static class UdfTitleResolver
    {
        private const string BlurayPlaylistDir = @"BDMV\PLAYLIST";
        private const string BlurayStreamDir = @"BDMV\STREAM";
        private const string DvdVideoTsDir = @"VIDEO_TS";
        private const string DvdMainTitle = @"VIDEO_TS\VTS_01_1.VOB";
        private const double TicksPerSecond = 45000.0;

        internal sealed class TitleInfo
        {
            /// <summary>蓝光碟片（true）还是 DVD（false）。</summary>
            public bool IsBluray;

            /// <summary>碟内相对路径（统一以 <c>\</c> 分隔），如 <c>BDMV\STREAM\00001.m2ts</c>。</summary>
            public string RelativePath;

            /// <summary>选中的 playlist 名（如 00001.mpls）或 VOB 名，仅用于日志。</summary>
            public string SourceName;

            /// <summary>蓝光为 playlist 总时长（秒）；无法计算时为 0。</summary>
            public double DurationSeconds;
        }

        /// <summary>打开本地 ISO 文件解析正片；失败返回 null。</summary>
        public static TitleInfo Resolve(string isoPath)
        {
            try
            {
                using var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);

                using (var udf = new UdfReader(stream))
                {
                    return Resolve(new UdfDiscFs(udf));
                }
            }
            catch (Exception e)
            {
                Log($"resolve failed for {isoPath}: {e.Message}");
                return null;
            }
        }

        /// <summary>在已挂载的卷根目录（真实文件系统）上解析正片；失败返回 null。</summary>
        public static TitleInfo ResolveFromMount(string driveRoot)
        {
            try
            {
                return Resolve(new LocalDiscFs(driveRoot));
            }
            catch (Exception e)
            {
                Log($"resolve failed for mounted {driveRoot}: {e.Message}");
                return null;
            }
        }

        private static TitleInfo Resolve(IDiscFs fs)
        {
            if (fs.DirectoryExists(BlurayPlaylistDir) && fs.DirectoryExists(BlurayStreamDir))
            {
                var bluray = ResolveBluray(fs);
                if (bluray != null) return bluray;
            }

            if (fs.DirectoryExists(DvdVideoTsDir))
            {
                var dvd = ResolveDvd(fs);
                if (dvd != null) return dvd;
            }

            return null;
        }

        private static TitleInfo ResolveBluray(IDiscFs fs)
        {
            PlaylistEntry best = null;

            foreach (var path in fs.GetFiles(BlurayPlaylistDir))
            {
                if (!path.EndsWith(".mpls", StringComparison.OrdinalIgnoreCase)) continue;

                PlaylistEntry entry = null;
                try
                {
                    entry = ParsePlaylist(LeafOf(path), fs.ReadAllBytes(path));
                }
                catch (Exception e)
                {
                    Log($"playlist parse failed for {path}: {e.Message}");
                }

                if (entry == null || entry.Clips.Count == 0) continue;

                if (best == null || entry.TotalSeconds > best.TotalSeconds) best = entry;
            }

            if (best != null)
            {
                var clip = best.Clips.OrderByDescending(c => c.Duration).First();
                var relative = BlurayStreamDir + "\\" + clip.Name + ".m2ts";

                if (fs.FileExists(relative))
                {
                    return new TitleInfo
                    {
                        IsBluray = true,
                        RelativePath = relative,
                        SourceName = best.Name,
                        DurationSeconds = best.TotalSeconds
                    };
                }

                Log($"selected stream {relative} not found; falling back to largest m2ts");
            }

            return LargestFile(fs, BlurayStreamDir, ".m2ts", true);
        }

        private static TitleInfo ResolveDvd(IDiscFs fs)
        {
            if (fs.FileExists(DvdMainTitle))
            {
                return new TitleInfo
                {
                    IsBluray = false,
                    RelativePath = DvdMainTitle,
                    SourceName = LeafOf(DvdMainTitle)
                };
            }

            return LargestFile(fs, DvdVideoTsDir, ".vob", false);
        }

        /// <summary>兜底：目录内体积最大的指定扩展名文件。</summary>
        private static TitleInfo LargestFile(IDiscFs fs, string dir, string extension, bool isBluray)
        {
            var largest = fs.GetFiles(dir)
                .Where(p => p.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                .Select(p => new { Path = p, Size = fs.GetFileLength(p) })
                .Where(x => x.Size > 0)
                .OrderByDescending(x => x.Size)
                .FirstOrDefault();

            if (largest == null) return null;

            return new TitleInfo
            {
                IsBluray = isBluray,
                RelativePath = largest.Path,
                SourceName = LeafOf(largest.Path)
            };
        }

        private sealed class PlaylistEntry
        {
            public string Name;
            public double TotalSeconds;
            public List<ClipEntry> Clips = new List<ClipEntry>();
        }

        private sealed class ClipEntry
        {
            public string Name;
            public double Duration;
        }

        /// <summary>
        /// 轻量 MPLS 解析（MPLS0100/0200/0300）：
        /// 头 8 字节 magic + <c>PlayList_start_address</c>（偏移 8，大端 4 字节）；
        /// PlayList = 长度4 + 保留2 + PlayItem 数2 + SubPath 数2；
        /// 每个 PlayItem = 长度2 + Clip 名5 + codec4 + 保留1 + 标志/still2 + in_time4 + out_time4（其后按长度跳过）。
        /// in/out 时间戳为 45kHz。本实现只取「playlist 总时长」与「最长 PlayItem 的 Clip 名」，不解析流表。
        /// </summary>
        private static PlaylistEntry ParsePlaylist(string name, byte[] data)
        {
            if (data == null || data.Length < 40) return null;

            var magic = Encoding.ASCII.GetString(data, 0, 8);
            if (magic != "MPLS0100" && magic != "MPLS0200" && magic != "MPLS0300") return null;

            var pos = 8;
            var playlistStart = ReadUInt32(data, ref pos);
            if (playlistStart + 10u > (uint)data.Length) return null;

            pos = (int)playlistStart;
            ReadUInt32(data, ref pos);                  // PlayList 长度
            ReadUInt16(data, ref pos);                  // 保留
            var itemCount = ReadUInt16(data, ref pos);  // PlayItem 数
            ReadUInt16(data, ref pos);                  // SubPath 数

            var entry = new PlaylistEntry { Name = name };

            for (var i = 0; i < itemCount; i++)
            {
                if (pos + 22 > data.Length) return null;

                var itemStart = pos;
                var itemLength = ReadUInt16(data, ref pos);
                var clipName = ReadAscii(data, 5, ref pos);
                ReadAscii(data, 4, ref pos);            // codec identifier，正常为 M2TS
                pos++;                                  // 保留
                pos += 2;                               // 标志（bit4 = 多角度）+ still mode

                var inTime = ReadUInt32(data, ref pos) / TicksPerSecond;
                var outTime = ReadUInt32(data, ref pos) / TicksPerSecond;
                var duration = Math.Max(0, outTime - inTime);

                entry.Clips.Add(new ClipEntry { Name = clipName, Duration = duration });
                entry.TotalSeconds += duration;

                var next = itemStart + 2 + itemLength;
                if (next <= itemStart || next > data.Length) return null;
                pos = next;
            }

            return entry;
        }

        private static ushort ReadUInt16(byte[] data, ref int pos)
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos, 2));
            pos += 2;
            return value;
        }

        private static uint ReadUInt32(byte[] data, ref int pos)
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos, 4));
            pos += 4;
            return value;
        }

        private static string ReadAscii(byte[] data, int count, ref int pos)
        {
            var value = Encoding.ASCII.GetString(data, pos, count);
            pos += count;
            return value;
        }

        /// <summary>从已归一化为 <c>\</c> 分隔的相对路径里取文件名。</summary>
        private static string LeafOf(string path)
        {
            var index = path.LastIndexOf('\\');
            return index >= 0 ? path.Substring(index + 1) : path;
        }

        private static void Log(string message)
        {
            try
            {
                Plugin.Instance?.Logger?.Debug("UdfTitleResolver - " + message);
            }
            catch
            {
                // 忽略
            }
        }

        /// <summary>碟内文件系统的最小抽象：进程内 UDF 与已挂载卷各实现一份，路径统一为 <c>\</c> 分隔的相对路径。</summary>
        private interface IDiscFs
        {
            bool DirectoryExists(string relativeDir);
            bool FileExists(string relativePath);
            string[] GetFiles(string relativeDir);
            long GetFileLength(string relativePath);
            byte[] ReadAllBytes(string relativePath);
        }

        private sealed class UdfDiscFs : IDiscFs
        {
            private readonly UdfReader _udf;

            public UdfDiscFs(UdfReader udf)
            {
                _udf = udf;
            }

            public bool DirectoryExists(string relativeDir) => _udf.DirectoryExists(relativeDir);

            public bool FileExists(string relativePath) => _udf.FileExists(relativePath);

            public string[] GetFiles(string relativeDir)
            {
                try
                {
                    return _udf.GetFiles(relativeDir, "*").Select(Normalize).ToArray();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            public long GetFileLength(string relativePath)
            {
                try
                {
                    return _udf.GetFileLength(relativePath);
                }
                catch
                {
                    return 0;
                }
            }

            public byte[] ReadAllBytes(string relativePath)
            {
                using var source = _udf.OpenFile(relativePath, FileMode.Open, FileAccess.Read);
                using var buffer = new MemoryStream();
                source.CopyTo(buffer);
                return buffer.ToArray();
            }

            private static string Normalize(string path) => path.Replace('/', '\\').TrimStart('\\');
        }

        private sealed class LocalDiscFs : IDiscFs
        {
            private readonly string _root;

            public LocalDiscFs(string driveRoot)
            {
                _root = driveRoot.EndsWith("\\") || driveRoot.EndsWith("/") ? driveRoot : driveRoot + "\\";
            }

            public bool DirectoryExists(string relativeDir) => Directory.Exists(ToLocal(relativeDir));

            public bool FileExists(string relativePath) => File.Exists(ToLocal(relativePath));

            public string[] GetFiles(string relativeDir)
            {
                var dir = ToLocal(relativeDir);

                try
                {
                    return Directory.Exists(dir) ? Directory.EnumerateFiles(dir).Select(Normalize).ToArray() : Array.Empty<string>();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            public long GetFileLength(string relativePath)
            {
                try
                {
                    var info = new FileInfo(ToLocal(relativePath));
                    return info.Exists ? info.Length : 0;
                }
                catch
                {
                    return 0;
                }
            }

            public byte[] ReadAllBytes(string relativePath) => File.ReadAllBytes(ToLocal(relativePath));

            private string ToLocal(string relativePath) => Path.Combine(_root, relativePath);

            private string Normalize(string fullPath)
            {
                var relative = fullPath.StartsWith(_root, StringComparison.Ordinal)
                    ? fullPath.Substring(_root.Length)
                    : fullPath;

                return relative.Replace('/', '\\').TrimStart('\\');
            }
        }
    }
}
