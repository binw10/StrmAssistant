# ISO 链路

ISO 原盘是 Emby 原生最弱的一环：它既不认 `.iso` 里的正片，也不认 MPLS 播放列表章节。
本插件补三件事：**定位正片 → 提取媒体信息 → 补写章节**（截图仅本地 ISO 支持）。

代码分布：`Mod/IsoExtraction.cs`（补丁）、`Common/IsoMounter.cs`（挂载/缓存）、
`Common/UdfTitleResolver.cs`（UDF 解析）、`Common/HttpRangeStream.cs`（远程流）。

## 两条路径

```
本地 .iso
  Windows ──> OS 挂载（PowerShell Mount-DiskImage）──> 盘符上的 BDMV\PLAYLIST\*.mpls
  Linux/Docker ──> 插件内置 DiscUtils.Udf 直读（需配合自定义 ffprobe）

远程 .iso（strm 里写 http URL）
  HttpRangeStream（Range 分块读）──> DiscUtils.Udf ──> MPLS 章节
  ⚠️ 远程截图不支持
```

Windows 上**优先用挂载**，因为挂载后路径是普通文件路径，Emby 原生 ffprobe 就能读；
Linux 上没有挂载能力，只能进程内 UDF 解析。

## 远程流：`HttpRangeStream`

| 特性 | 说明 |
|---|---|
| `Length` | 必须是**真实总长**。DiscUtils 会校验分区长度 ≤ 流长度，给个 8MB 窗口会直接抛 `Substream extends beyond end of parent stream` —— 这是早期放弃远程 ISO 的真正原因 |
| 块策略 | 1MB / 块，64 块 LRU（约 64MB 上限），只拉 UDF 元数据区 |
| 重试 | 单块 3 次 + 总预算 90s，超预算返回短读让上层静默降级 |
| **必须验 Range** | 先 HEAD 取长度，再发 `bytes=0-0` **确认返回 206**。只信 `Content-Length` 不够 —— 不支持 Range 的服务器会回 200 全量，白拉整个文件 |

章节结果按 URL 缓存 30 分钟（`IsoMounter.TryGetRemoteChapters`），清扫器一并清理。

## MPLS 解析

`UdfTitleResolver` 从 `BDMV/PLAYLIST/*.mpls` 里挑时长最长的作为正片，再解析 `PlayListMark` 取章节。

**`PlayListMark` 结构（易错）**：

```
长度 4 字节  +  mark 数 2 字节  +  每条 mark 14 字节
                                  ├ reserved  1
                                  ├ mark_type 1
                                  ├ ref       2
                                  ├ timestamp 4   ← 45kHz 绝对 PTS
                                  ├ duration  4
                                  └ pid       2
```

- **长度字段后面没有保留字节**。保留字节在每条 mark **内部**（就是上面那个 `reserved`）。
- 长度自洽：`length == 2 + 14 × count`。
- `timestamp` 是 **45kHz 绝对 PTS**，要减去该 PlayItem 的 `IN_time`，再加上前面 PlayItem 的累计时长，
  才是最终章节位置。不做这个换算，多 PlayItem 的盘章节会整体偏移。

## 🔴 DiscUtils 的 searchPattern 陷阱

```csharp
_udf.GetFiles(dir, "*")      // ← 实测返回 0 条！
_udf.GetFiles(dir, "*.*")    // ← 正确
```

`"*"` **不匹配带扩展名的文件**。实测 `BDMV\PLAYLIST` 下 `"*"` → 0，`"*.*"` / `"*.mpls"` → 1。

这个坑让 `UdfTitleResolver.Resolve(path)` 长期返回 null（进程内 UDF 从未真正工作过），
Windows 上被挂载路径 `LocalDiscFs` 掩盖，所以一直没暴露。

**结论：凡是 DiscUtils 的 searchPattern，一律用 `*.*`，或者用 `GetFileSystemEntries` + `FileExists` 过滤。**

## 截图

- 本地 ISO：支持。先删海报，再 `imageRefreshMode=FullRefresh&replaceAllImages=true`，并临时开 `EnableImageCapture`（改 JSON + 重启）。
- 远程 ISO：**不支持**。ffmpeg 喂不了 Range 流，要做就得先落"稀疏缓存文件"，成本翻倍，暂不做。

## 触发验证

```
❌ POST /emby/Items/{id}/Refresh
   → .strm / shortcut 条目，BaseMediaInfoProber.Probe 里有
     if (!item.IsShortcut || refreshOptions.EnableRemoteContentProbe) 会跳过探测

✅ POST /emby/ScheduledTasks/Running/f84e5d989aa8a8b6ab1a3a8c0848faab   （提取媒体信息任务）
   → 只有插件流程 GetMediaInfoRefreshOptions() 里把 EnableRemoteContentProbe 置 true
```

日志关键字：

```
remote iso probing via downloaded window
remote playlist chapters resolved for ... : N
```

落库确认：`Chapters3` 表（**不是 `Chapters2`**），`select count(*) from Chapters3 where ItemId=<id>`。

## 与 PRO / wt 的方案对比

三个项目解决的是同一个问题（Emby 不认 ISO 里的正片），但路线差别很大。
调研时间 2026-10-22，依据：PRO `3.0.0.56` 反编译、wt `v1.0.0.45` 反编译、本仓源码。

> ⚠️ **可信度分级**：下表里标「已确认」的是在反编译代码里直接读到的；
> 标「未能确认」的是字符串/类型名推断，PRO 经过 ConfuserEx **字符串加密 + 控制流平坦化**，
> 只看得到骨架，不能当结论用。

| | 本仓 | PRO `3.0.0.56` | wt `v1.0.0.45` |
|---|---|---|---|
| **读盘方式** | Windows：PowerShell `Mount-DiskImage` → `Get-Volume` 取盘符（已确认）；Linux/Docker：进程内 `DiscUtils.Udf`，**探测输入仍指向原 ISO**（已确认） | 进程内 `new UdfReader(stream)`，`stream` 来自 `fileSystem.OpenRead`（已确认）；另有 `AsyncLocal<UdfReader>` 让 ISO 内文件可被当普通文件读（已确认）。**是否在 Windows 上挂载：未能确认** | **不挂载**。把 `MediaSource.ProbePath` 改成 `"bluray:" + iso路径`、`ProbeProtocol=File`，同时把 ffprobe 可执行文件换成用户配的 `CustomFfprobePath`（已确认） |
| **媒体信息从哪来** | 挂载后交 Emby 原生 ffprobe 探测真实 `.m2ts`，编码/码率/HDR/音轨/字幕全有（已确认） | `BdInfoExaminer(BaseItem.FileSystem).GetDiscInfo(path)` + `BDROM` / `TSPlaylistFile` / `StreamFiles`（BDInfo 全套，已确认）；ffprobe 仍跑（`ProbeFormat.format`），最后把 `Container` 覆写成 `Iso` | ffprobe 走 `bluray:` 协议（依赖用户 ffprobe 的 libbluray 能读 UDF），再用自研 `BlurayMplsParser` / `BlurayStructureReader` 在 postfix 里补章节与语言 |
| **打点位置** | `MediaProbeManager.GetMediaInfoInternal` Prefix/Postfix | `FFProbeProvider.IsSupported` + 文件打开补丁 | `FFProbeProvider.IsSupported` + `FfRunnerBase.OnBeforeStartProcess`（换 ffprobe）+ `MediaProbeManager.GetMediaInfo` Prefix（改 `ProbePath`）/Postfix（补章节语言） |
| **远程 ISO（strm→http）** | ✅ `HttpRangeStream` + DiscUtils，只补章节 | 未见 | 未见（`LooksLikeIsoPath` 只认本地 `.iso`） |
| **对用户的前置要求** | Windows 无；Linux/Docker 需配自定义 ffprobe | 无（自带盘片解析） | **必须自备支持 `bluray:` 的 ffprobe** |

### 关键差异：wt 走的正是我们放弃的那条路

wt 的 `BuildProbePath` 只有一行实质逻辑：

```csharp
if (container == "blurayiso") return "bluray:" + path;
```

也就是说它**没有自己读盘**，而是让 ffprobe 用 `bluray:` 协议去读 —— 这正是
[ADR 0003](../decisions/0003-放弃-bluray-协议.md) 里我们实测放弃的方案。
差别只在"谁提供 libbluray 能力"：我们试的是 Emby 内置 ffprobe（Windows 上读不了 UDF），
wt 让用户自己指一个 `CustomFfprobePath`（在 Linux 上大概率能跑，Windows 上仍不行）。

所以 wt 的可用性完全取决于用户配的那个 ffprobe，配不对就等于没有这个功能。

### PRO 最完整，但代价也最大

`BdInfoExaminer` + `BDROM` / `TSPlaylistFile` 是 BDInfo 那一套完整盘片解析，
不依赖 ffprobe 也能拿到 playlist 级的音轨/字幕/章节/时长 —— 这是它能在 Linux/Docker 出结果的原因。
代价是：体量大，且要靠 `AsyncLocal<UdfReader>` + 文件打开补丁把 ISO 内部文件伪装成普通路径，
侵入面广（Emby 任何读文件的代码都可能走到这条补丁上）。

### 本仓的真实短板

- Windows 挂载路径**信息最全**（原生 ffprobe 读真 m2ts），这是三者里最好的；
- Linux/Docker 只做了"进程内 UDF 解析正片"，但**探测输入没改**，所以仍然指望用户配的 ffprobe 能读 ISO；
- 远程 ISO 只有章节，没有媒体信息。

### 本仓已解析但被丢掉的数据

`UdfTitleResolver.ResolveBluray` 已经把选中 playlist 的 `STN_table` 解出来了：

```csharp
AudioStreams    = streams.Where(s => s.Kind == BlurayStreamKind.Audio).ToList(),
SubtitleStreams = streams.Where(s => s.Kind == BlurayStreamKind.Subtitle).ToList(),
Chapters        = chapters,
DurationSeconds = best.TotalSeconds,
```

但 `IsoMounter.RememberChapters` 只把 `info.Chapters` 存进 `TitleChapters` 缓存，
**`AudioStreams` / `SubtitleStreams` / `DurationSeconds` 全部丢弃**；`IsoExtraction` 也只注入 `ChapterInfo`。

这块数据正好补两个短板，但**不能直接合成完整的 MediaStreams** —— 见下节。

## 改进路线（方案，待批准）

### P0 · 让 Linux 内置 ffprobe 真正读 ISO（新发现，优先于 P1）

**先厘清一件事：不是 ffprobe 不能读，是我们没让它用正确的方式读。**

ffprobe 读 ISO 有两条完全不同的路：

| 输入 | 行为 |
|---|---|
| `ffprobe -i /path/movie.iso` | 把裸 ISO 当 **mpegts** 解 —— UDF 头不是合法 TS，基本拿不到东西 |
| `ffprobe -i 'bluray:/path/movie.iso'` | 走 libbluray → libudfread 读 UDF → 按 playlist 选出正片，**这才是读盘** |

Emby 内置 ffprobe（`5.1-emby_2023_06_25_p6`）的能力实测：

```
$ ffprobe -buildconf | grep bluray
    --enable-libbluray
$ ffprobe -protocols | grep bluray
  bluray
$ strings ffprobe.exe | grep udfread_ | sort -u
udfread_file_openat
udfread_opendir_at
```

也就是说**内置 ffprobe 同时具备 `bluray` 协议和 libudfread**。
（Windows 版二进制里两者都在，但 ADR 0003 实测 `bluray:` 在 Windows 读不了 UDF —— 这是平台差异，
不是能力缺失。Linux 上 libudfread 读普通文件是常规路径，同一套构建流水线大概率同样带这两个能力，
**但需要一次 Linux 实机确认**。）

**当前代码的实际行为**：

1. `IsoExtraction.IsSupportedPrefix` 已经把 `.iso` 放行 → Emby 会尝试探测；
2. 但 Linux 上 `IsoMounter.GetProbePath` 返回 `null`（只有 Windows 挂载成功才有值），
   → **探测输入仍是裸 `.iso` 路径**，走的是上表第一行，必然读不出来。

**并且这里有个已声明未实现的开关**：

```csharp
// Common/FfmpegPathOverride.cs
/// IsoExtraction 据此决定是否把探测输入改写为 bluray:"{0}"
public static bool? ProbeSupportsBluray { get; private set; }
```

`ProbeSupportsBluray` 有声明、有赋值（自检时探测 `--enable-libbluray`）、有注释说明用途，
但 `grep ProbeSupportsBluray` 全文**只有这三处，没有任何消费点** ——
`IsoExtraction` 从来没读过它。所以「按能力位决定是否改写成 `bluray:`」这条路**根本没接线**。

**建议做法**：

- Linux/Docker 分支：若探测到 ffprobe 支持 `bluray`（内置的按 buildconf 判定，或自定义的按
  `ProbeSupportsBluray` 判定），把 `inputPath` 改写为 `bluray:"{isoPath}"`；
- Windows 分支保持现状（挂载），不启用 `bluray:`（ADR 0003 已实测失败）；
- 失败要能静默回退到裸路径，不能把 ISO 条目的探测整个搞挂。

**收益**：Linux/Docker 用户**不需要配任何自定义 ffprobe** 就能提取 ISO 媒体信息 ——
这比 P1 影响面大得多。

**待确认**（必须先做，否则不能动代码）：

1. 找一台 Linux（或 Docker）跑 `ffprobe -protocols | grep bluray` 与 `-buildconf`，确认内置版是否带 libbluray；
2. 拿一个真 BD ISO 跑 `ffprobe -i 'bluray:/path/x.iso'`，确认能出正片流；
3. 确认 libbluray 自带的章节与我们的 MPLS 解析是否冲突（两边都给章节，别重复注入）。

### 先划清能力边界：STN 能提供什么

`MplsStreamInfo` 的字段只有：`Pid` / `Kind` / `Coding` / `CodingRaw` / `Language` / `Channels` / `SampleRate`。

| 能填 | 不能填 |
|---|---|
| 音轨：语言、编码（AC3/DTS/TrueHD…）、声道、采样率 | **视频流**（STN 只映射了 Audio/Subtitle） |
| 字幕：语言、编码（PGS） | 分辨率、码率、HDR 信息 |

所以**不存在"用 STN 合成完整媒体信息"这条路**。上一版文档里"补上之后远程 ISO 就能给出结构完整的
媒体信息"说过头了，实际只能补音轨/字幕那几项。

### P1 · 用 STN 补音轨 / 字幕的语言标签（推荐先做）

**问题**：Windows 挂载后 ffprobe 读的是 `.m2ts`，而 m2ts 码流里**不带语言标签**
（语言写在 MPLS 的 STN_table 里），所以 Emby 里音轨/字幕全显示"未知"。

**做法**：`IsoExtraction` 的 postfix 在拿到 ffprobe 结果后，把 STN 的 audio/subtitle
按**顺序**对齐到 `MediaStreams` 里对应的流，只补 `Language`（必要时补 `Codec` / `Channels`）。

**约束**：两边数量不相等就**放弃对齐**（宁可不补，也不要标错语言）。
ffprobe 输出里没有 PID，无法做精确匹配。

**收益**：Windows 用户能直接看到多音轨的语言标签——这是现在最容易被吐槽的点。

### P2 · 远程 ISO / 无 ffprobe 时补时长

`TitleInfo.DurationSeconds` 是 MPLS 各 PlayItem 时长之和，可靠。
探测结果 `RunTimeTicks` 为空时用它补上，让 Emby 至少显示正确时长、进度条正常。

**不做**：不合成 MediaStreams。没有视频流信息的情况下硬造流，会让 Emby 的转码/直连决策出错。

### P3 · Linux/Docker 把话说清楚（P0 落地后可取消）

Linux 只有两条路：让 ffprobe 用 `bluray:` 读（P0），或把 m2ts 导出来（几十 GB，不现实）。

- **P0 成功** → P3 取消，不需要再要求用户配自定义 ffprobe；
- **P0 失败**（内置 ffprobe 在 Linux 上也读不了） → 退回到只改体验：检测到
  「ISO 条目 + 未配 `CustomProbePath`」时打一条 Info，说明为什么没有媒体信息，别让用户以为功能坏了。

### 明确不做

| 方案 | 为什么不做 |
|---|---|
| 学 wt 用 `bluray:` 协议 | 就是 [ADR 0003](../decisions/0003-放弃-bluray-协议.md) 实测放弃的路；wt 只是把它外包给用户的 ffprobe |
| 学 PRO 引入 BDInfo（`BdInfoExaminer` / `BDROM`） | 我们已有 MPLS + STN 自研解析，重复建设；BDInfo 只为补"视频流信息"这一项，代价过大 |
| 学 PRO 的 `AsyncLocal<UdfReader>` + 文件打开补丁 | 那是为了让 Emby 真的读 ISO 内文件；我们只需要探测结果，不需要让 Emby 播放 ISO 内部路径 |



## 相关决策

- 为什么放弃 `bluray:` 协议 → [../decisions/0003-放弃-bluray-协议.md](../decisions/0003-放弃-bluray-协议.md)
- 远程 ISO 的完整实施记录 → `history/2026-10/2026-10-20-优化方案-实测版.md`
