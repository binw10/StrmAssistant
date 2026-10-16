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

            /// <summary>MPLS STN_table 解析出的主音轨（语言/编码/声道）；可能为空。</summary>
            public List<MplsStreamInfo> AudioStreams = new List<MplsStreamInfo>();

            /// <summary>MPLS STN_table 解析出的字幕（语言）；可能为空。</summary>
            public List<MplsStreamInfo> SubtitleStreams = new List<MplsStreamInfo>();

            /// <summary>MPLS PlayListMark 解析出的章节（秒，升序，首个通常为 0）；可能为空。</summary>
            public List<double> Chapters = new List<double>();
        }

        /// <summary>
        /// STN_table 中一条基本流（primary audio / PG / IG / secondary 等）的语言与编码信息。
        /// 仅承载 MPLS 能提供的字段；ffprobe 侧的宽度/高度/时长等由探测器补充。
        /// </summary>
        internal sealed class MplsStreamInfo
        {
            /// <summary>基本流 PID（包标识）。</summary>
            public int Pid;

            /// <summary>BD-ROM 层面的流类型，如 Audio/Subtitle。</summary>
            public BlurayStreamKind Kind;

            /// <summary>编码类型（coding_type），如 AC3/DTS/TrueHD/PG 等。</summary>
            public BlurayCodingType Coding;

            /// <summary>编码类型原始值（codint_type 4bit）。</summary>
            public int CodingRaw;

            /// <summary>语言码（ISO 639-2，3 字符，小写）；未知为 "und" 或空。</summary>
            public string Language;

            /// <summary>音频：声道数（1~8），未知为 0。</summary>
            public int Channels;

            /// <summary>音频：采样率（Hz），未知为 0。</summary>
            public int SampleRate;

            public override string ToString() =>
                $"{Kind}/{Coding} pid={Pid} lang={Language} ch={Channels}";
        }

        internal enum BlurayStreamKind
        {
            Unknown = 0,
            Audio = 1,
            Subtitle = 2,
            Interactive = 3,
            Text = 4
        }

        internal enum BlurayCodingType
        {
            Unknown = 0,
            Ac3,
            Dts,
            TrueHd,
            Eac3,
            Pcm,     // LPCM
            DtsHd,
            DtsHdMa,
            DtsHdHra,
            DtsHdLbr,
            DtsExpress,
            Dsd,
            Pg,      // PGS 字幕
            Ig,      // 交互图形
            Text,    // ASCII text subtitle
            Other
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

                // 选中的 playlist 携带的 STN_table / 章节，供上层补全音轨、字幕与章节
                var streams = best.Streams;
                var chapters = best.Chapters;

                if (fs.FileExists(relative))
                {
                    return new TitleInfo
                    {
                        IsBluray = true,
                        RelativePath = relative,
                        SourceName = best.Name,
                        DurationSeconds = best.TotalSeconds,
                        AudioStreams = streams.Where(s => s.Kind == BlurayStreamKind.Audio).ToList(),
                        SubtitleStreams = streams.Where(s => s.Kind == BlurayStreamKind.Subtitle).ToList(),
                        Chapters = chapters
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

            /// <summary>STN_table 中解析出的基本流（音轨 + 字幕）。</summary>
            public List<MplsStreamInfo> Streams = new List<MplsStreamInfo>();

            /// <summary>PlayListMark 解析出的章节时间点（秒，升序）。</summary>
            public List<double> Chapters = new List<double>();
        }

        private sealed class ClipEntry
        {
            public string Name;
            public double Duration;

            /// <summary>
            /// 该 PlayItem 的 IN_time（45kHz → 秒，**绝对值**，含 m2ts 自身的 PTS 基址）。
            /// 章节时间戳同为绝对值，需减去本值才是标题内偏移。
            /// </summary>
            public double InTime;

            /// <summary>该 PlayItem 在 MPLS 文件内的绝对起始偏移（指向 itemLength 字段）。</summary>
            public int ItemStart;
        }

        /// <summary>
        /// 轻量 MPLS 解析（MPLS0100/0200/0300）。
        ///
        /// 头部（8 字节 magic 之后均为大端）：
        /// <c>PlayList_start_address</c>(4) <c>PlayListMark_start_address</c>(4) ...
        /// PlayList = 长度4 + 保留2 + PlayItem 数2 + SubPath 数2；
        /// 每个 PlayItem = 长度2 + Clip 名5 + codec4 + 保留1 + 标志/still2 + in_time4 + out_time4
        ///                + STN_table_start_address2 + STN_table_length2 + ...（其后按长度跳过）。
        /// STN_table = 长度2 + 保留2 + 基本流条目数2 + 每条目 [长度2 + 保留1 + 流PID2 + 子路径数1 + 子路径项...]
        ///             + 各流的 Stream_attributes（音频/字幕语言、编码、声道等）。
        /// PlayListMark = 长度4 + 保留1 + mark 数2 + 每条 [保留1 + 标志1 + mark_type1 + PlayItem_ref2
        ///                + timestamp4 + duration4 + entry_ES_PID2]。
        /// 所有 MPLS 时间戳为 45kHz。
        /// </summary>
        private static PlaylistEntry ParsePlaylist(string name, byte[] data)
        {
            if (data == null || data.Length < 40) return null;

            var magic = Encoding.ASCII.GetString(data, 0, 8);
            if (magic != "MPLS0100" && magic != "MPLS0200" && magic != "MPLS0300") return null;

            var pos = 8;
            var playlistStart = ReadUInt32(data, ref pos);
            var playlistMarkStart = ReadUInt32(data, ref pos);
            if (playlistStart + 10u > (uint)data.Length) return null;

            var entry = new PlaylistEntry { Name = name };

            // ---- PlayList（PlayItem 列表 + 各 item 的 STN_table）----
            pos = (int)playlistStart;
            ReadUInt32(data, ref pos);                  // PlayList 长度
            ReadUInt16(data, ref pos);                  // 保留
            var itemCount = ReadUInt16(data, ref pos);  // PlayItem 数
            ReadUInt16(data, ref pos);                  // SubPath 数

            for (var i = 0; i < itemCount; i++)
            {
                if (pos + 22 > data.Length) return null;

                var itemStart = pos;
                var itemLength = ReadUInt16(data, ref pos);   // PlayItem 长度（含其后的 STN_table）
                var clipName = ReadAscii(data, 5, ref pos);
                ReadAscii(data, 4, ref pos);                  // codec identifier，正常为 M2TS
                pos++;                                        // 保留
                pos += 2;                                     // 标志（bit4 = 多角度）+ still mode

                var inTime = ReadUInt32(data, ref pos) / TicksPerSecond;
                var outTime = ReadUInt32(data, ref pos) / TicksPerSecond;
                var duration = Math.Max(0, outTime - inTime);

                if (pos + 4 > data.Length) return null;
                var stnStart = ReadUInt16(data, ref pos);     // STN_table_start_address（相对 PlayItem 起点）
                var stnLength = ReadUInt16(data, ref pos);    // STN_table_length

                entry.Clips.Add(new ClipEntry
                {
                    Name = clipName,
                    Duration = duration,
                    InTime = inTime,
                    ItemStart = itemStart
                });
                entry.TotalSeconds += duration;

                // 只解析最长 PlayItem 的 STN_table（正片的流表；多 PlayItem 时后续项通常为分部）
                if (stnLength > 0)
                {
                    try
                    {
                        ParseStnTable(data, itemStart + stnStart, stnLength, entry.Streams);
                    }
                    catch (Exception e)
                    {
                        Log($"STN_table parse failed in {name}: {e.Message}");
                    }
                }

                var next = itemStart + 2 + itemLength;
                if (next <= itemStart || next > data.Length) return null;
                pos = next;
            }

            // ---- PlayListMark（章节）----
            if (playlistMarkStart > 0 && playlistMarkStart + 6u < (uint)data.Length)
            {
                try
                {
                    ParsePlayListMark(data, (int)playlistMarkStart, entry);
                }
                catch (Exception e)
                {
                    Log($"PlayListMark parse failed in {name}: {e.Message}");
                }
            }

            return entry;
        }

        // ================= STN_table =================

        /// <summary>
        /// 解析 STN_table：
        /// 长度2 + 保留2 + 基本流条目数 num_stream2，接着 num_stream 个
        /// [stream_entry 长度2 + 保留1 + 流PID2 + 子路径数1 + (子路径项 长度1 + 类型1 + 保留1 + 子Clip名5 + codec4)]
        /// 之后依次是 stream_attributes（与前面流一一对应）：
        /// 音频 Stream_attributes：长度1 + 保留1 + coding_type(4bit)/format(4bit) + format/rate(4bit)/channel(4bit) + lang3
        ///                         + 保留2（+ 若 coding=0x5(DTS_HD) 等再带 extension，见下）
        /// PG/Text Stream_attributes：长度1 + 保留1 + coding_type(高4bit)/... + lang3
        /// </summary>
        private static void ParseStnTable(byte[] data, int stnStart, int stnLength, List<MplsStreamInfo> output)
        {
            if (stnStart <= 0 || stnStart >= data.Length) return;
            var end = Math.Min(stnStart + stnLength, data.Length);

            var pos = stnStart;
            if (pos + 6 > end) return;

            ReadUInt16(data, ref pos);                 // STN_table 长度
            ReadUInt16(data, ref pos);                 // 保留
            var numStream = ReadUInt16(data, ref pos); // 基本流条目数

            // 先收集 stream_entry 头部的 PID + 子路径标记
            var entries = new List<(int Pid, bool HasSubPath)>(numStream);

            for (var i = 0; i < numStream; i++)
            {
                if (pos + 4 > end) return;

                var entryLen = data[pos];   // stream_entry 长度（不含自身）
                pos += 1;
                pos += 1;                   // 保留
                var pid = ReadUInt16(data, ref pos);
                var subPathCount = pos < end ? data[pos] : 0;
                pos += 1;

                var hasSubPath = subPathCount > 0;

                // 跳过子路径项：每项 = 长度1 + 类型1 + 保留1 + 子Clip名5 + codec4 + refsToSTCseqID 变长
                for (var s = 0; s < subPathCount && pos < end; s++)
                {
                    var subLen = data[pos];  // 子路径项长度（不含自身）
                    pos += 1 + subLen;
                }

                entries.Add((pid, hasSubPath));

                // 条目整体长度校验（entry 内部已按字段推进，这里仅保护不越界）
                if (pos > end) pos = end;
                _ = entryLen;
            }

            if (pos + 2 > end) return;
            var numStream2 = ReadUInt16(data, ref pos); // 与 numStream 相同（冗余字段）

            // 逐条解析 stream_attributes，与 entries 一一对应
            for (var i = 0; i < entries.Count && i < numStream2; i++)
            {
                if (pos + 1 > end) return;

                var attrLen = data[pos];   // stream_attributes 长度（不含自身）
                var attrStart = pos + 1;
                var attrEnd = Math.Min(attrStart + attrLen, end);

                var info = ParseStreamAttributes(data, attrStart, attrEnd, entries[i].Pid);
                if (info != null) output.Add(info);

                pos = attrEnd;
                if (pos <= attrStart) return; // 防死循环
            }
        }

        /// <summary>
        /// 解析单条 stream_attributes：
        /// 保留1 + coding_type/format 字节 + (format/rate|channel) 字节 + 语言3。
        /// coding_type 高 4 位，低 4 位为 format。
        /// </summary>
        private static MplsStreamInfo ParseStreamAttributes(byte[] data, int start, int end, int pid)
        {
            if (start + 1 > end) return null;

            var pos = start;
            pos += 1;                                  // 保留
            if (pos + 1 > end) return null;

            var codingByte = pos < end ? data[pos++] : 0;    // coding_type(高4) | format(低4)
            var coding = (codingByte >> 4) & 0x0F;
            var format = codingByte & 0x0F;

            if (pos + 1 > end) return null;

            var second = pos < end ? data[pos++] : 0;        // format(高4) | rate(低4)  或  channel 相关
            var channelByte = (second >> 4) & 0x0F;
            var rateByte = second & 0x0F;

            var language = pos + 3 <= end ? ReadAscii(data, 3, ref pos) : string.Empty;
            language = NormalizeLanguage(language);

            var info = new MplsStreamInfo
            {
                Pid = pid,
                CodingRaw = coding,
                Language = language,
                Coding = MapCoding(coding, out var kind)
            };
            info.Kind = kind;

            switch (kind)
            {
                case BlurayStreamKind.Audio:
                    info.Channels = MapChannels(channelByte);
                    info.SampleRate = MapSampleRate(rateByte);
                    break;
                case BlurayStreamKind.Subtitle:
                case BlurayStreamKind.Text:
                    // 字幕：coding 高 4 位在部分版本位于 byte0，这里已取 codingByte
                    info.Coding = MapSubtitleCoding(coding, format);
                    info.Kind = BlurayStreamKind.Subtitle;
                    break;
            }

            return info;
        }

        private static BlurayCodingType MapCoding(int coding, out BlurayStreamKind kind)
        {
            switch (coding)
            {
                case 0x01: kind = BlurayStreamKind.Audio; return BlurayCodingType.Other;       // MPEG audio
                case 0x03: kind = BlurayStreamKind.Audio; return BlurayCodingType.Pcm;
                case 0x04: kind = BlurayStreamKind.Audio; return BlurayCodingType.Ac3;
                case 0x05: kind = BlurayStreamKind.Audio; return BlurayCodingType.Dts;
                case 0x06: kind = BlurayStreamKind.Audio; return BlurayCodingType.TrueHd;
                case 0x07: kind = BlurayStreamKind.Audio; return BlurayCodingType.Eac3;
                case 0x08: kind = BlurayStreamKind.Audio; return BlurayCodingType.DtsHdHra;
                case 0x09: kind = BlurayStreamKind.Audio; return BlurayCodingType.DtsHdMa;
                case 0x0A: kind = BlurayStreamKind.Audio; return BlurayCodingType.DtsHdLbr;
                case 0x0B: kind = BlurayStreamKind.Audio; return BlurayCodingType.DtsExpress;
                case 0x0C: kind = BlurayStreamKind.Audio; return BlurayCodingType.Dsd;
                case 0x90: kind = BlurayStreamKind.Subtitle; return BlurayCodingType.Pg;
                case 0x91: kind = BlurayStreamKind.Subtitle; return BlurayCodingType.Ig;
                case 0x92: kind = BlurayStreamKind.Text; return BlurayCodingType.Text;
                default: kind = BlurayStreamKind.Unknown; return BlurayCodingType.Unknown;
            }
        }

        private static BlurayCodingType MapSubtitleCoding(int coding, int format) => coding switch
        {
            0x90 => BlurayCodingType.Pg,
            0x91 => BlurayCodingType.Ig,
            0x92 => BlurayCodingType.Text,
            _ => BlurayCodingType.Pg
        };

        // ================= PlayListMark（章节） =================

        /// <summary>
        /// 解析 PlayListMark（BD-ROM MPLS）：
        /// <c>长度4 + mark 数2 + 每条 mark 14 字节</c>；
        /// 每条 mark = <c>保留1 + mark_type1 + PlayItem_ref2 + timestamp4 + duration4 + entry_ES_PID2</c>。
        /// mark_type = 1 为章节标记（entry mark）。其余（如 0=PlayList mark / link point）不计入章节。
        ///
        /// ⚠️ 易错点：PlayListMark **没有** PlayList() 那样的「长度后的保留字段」——紧跟长度的 2 字节
        /// 就是 mark 数（保留字节在**每条 mark 内部**）。实测样本 <c>00000.mpls</c>：
        /// <c>190:00 00 00 10 | 194:00 01 | 196:00 01 0000 0b43e940 ...</c> →
        /// 长度=16(=2+14×1)、mark 数=1、唯一 mark 为 entry mark@4200s。长度字段可作一致性校验
        /// （应满足 <c>length == 2 + 14×count</c>），不符则放弃解析，避免读到越界垃圾。
        ///
        /// 时间戳为 45kHz 的**绝对 PTS**（含 m2ts 自身 PTS 基址，实测样本基址 4200s），
        /// 故须按 <c>PlayItem_ref</c> 减去对应 PlayItem 的 IN_time，再加上其前面各 PlayItem 的累计时长，
        /// 才是 Emby 需要的「标题内偏移秒」。
        /// </summary>
        private static void ParsePlayListMark(byte[] data, int markStart, PlaylistEntry entry)
        {
            if (markStart <= 0 || markStart + 6 > data.Length) return;

            var pos = markStart;
            var sectionLength = ReadUInt32(data, ref pos);   // PlayListMark 长度（= 2 + 14×mark 数）
            if (pos + 2 > data.Length) return;
            var markCount = ReadUInt16(data, ref pos);       // mark 数（紧跟长度，无保留字节）

            // 一致性校验：长度字段必须与「2 + 14×count」吻合，否则视为结构不符，宁可不解析
            if (sectionLength != (uint)(2 + 14 * markCount)) return;

            var marks = new List<double>(markCount);

            for (var i = 0; i < markCount; i++)
            {
                if (pos + 14 > data.Length) break;

                pos += 1;                            // 保留（每条 mark 内部）
                var markType = data[pos++];          // mark_type
                var playItemRef = ReadUInt16(data, ref pos);
                var timestamp = ReadUInt32(data, ref pos) / TicksPerSecond;  // 绝对 PTS
                ReadUInt32(data, ref pos);           // duration
                ReadUInt16(data, ref pos);           // entry_ES_PID

                // mark_type: 1 = entry mark（章节）
                if (markType != 1) continue;

                var relative = ToTitleRelativeSeconds(entry, playItemRef, timestamp);
                if (relative >= 0) marks.Add(relative);
            }

            marks.Sort();

            // 去重：相邻 <1ms 视为同一章节（多 PlayItem 时首尾相接可能产生重复点）
            var deduped = new List<double>(marks.Count);
            foreach (var mark in marks)
            {
                if (deduped.Count == 0 || mark - deduped[deduped.Count - 1] > 0.001) deduped.Add(mark);
            }

            entry.Chapters = deduped;
        }

        /// <summary>
        /// 把 PlayListMark 的绝对 PTS 换算为「标题内偏移秒」：
        /// 前面各 PlayItem 的累计时长 + (mark 绝对时间 − 本 PlayItem 的 IN_time)。
        /// </summary>
        private static double ToTitleRelativeSeconds(PlaylistEntry entry, int playItemRef, double absoluteSeconds)
        {
            if (entry.Clips.Count == 0) return -1;

            // PlayItem_ref 越界时回退到第一个 PlayItem（单 PlayItem 盘最常见，ref 恒为 0）
            var index = playItemRef >= 0 && playItemRef < entry.Clips.Count ? playItemRef : 0;

            var cumulative = 0.0;
            for (var i = 0; i < index; i++) cumulative += entry.Clips[i].Duration;

            var relative = cumulative + (absoluteSeconds - entry.Clips[index].InTime);
            return relative < 0 ? 0 : relative;
        }

        // ================= 语言与属性映射 =================

        /// <summary>把 3 字符语言码规范化为小写；全 0 或非法返回 "und"。</summary>
        private static string NormalizeLanguage(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "und";

            var cleaned = raw.Trim('\0', ' ');
            if (cleaned.Length == 0) return "und";

            // 全 0 字节 / 非 ASCII 可打印 → 未知
            if (cleaned.All(c => c == '\0' || c < 0x21 || c > 0x7E)) return "und";

            var lower = cleaned.ToLowerInvariant();
            return lower == "und" ? "und" : lower;
        }

        /// <summary>音频声道数：BD 音频 attribute 的 channel 编码（0b0001=1ch ... 0b0111=8ch）。</summary>
        private static int MapChannels(int channelCode) => channelCode switch
        {
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            6 => 6,
            7 => 8,
            8 => 2,   // 立体声（按规范保留）
            _ => 0
        };

        /// <summary>音频采样率：rate code → Hz。</summary>
        private static int MapSampleRate(int rateCode) => rateCode switch
        {
            1 => 48000,
            4 => 96000,
            5 => 192000,
            _ => 0
        };

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
