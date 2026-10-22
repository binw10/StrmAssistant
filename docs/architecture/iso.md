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

| | 本仓 | PRO `3.0.0.56` | wt `v1.0.0.45` |
|---|---|---|---|
| **读盘方式** | Windows 系统挂载（PowerShell `Mount-DiskImage`）→ 盘符；Linux/Docker 进程内 `DiscUtils.Udf` | `IsoMounter` / `BlurayMounter`（有 `CanMount` 判断）+ 进程内 `UdfReader(stream)` | **不读盘**，把读盘能力外包给用户自备的 `CustomFfprobePath` |
| **媒体信息从哪来** | 挂载后交给 Emby 原生 ffprobe（编码/码率/HDR/音轨/字幕全有） | `BdInfoExaminer`（BDInfo 系）读 BDMV/MPLS/CLPI 得 playlist 结构；ffprobe 结果再覆写 `Container=Iso` | ffprobe 结果 + 自研 `BlurayStructureReader` / `BlurayMplsParser` 补章节与语言 |
| **打点位置** | `MediaProbeManager.GetMediaInfoInternal` 的 Prefix/Postfix（注入章节） | `FFProbeProvider.IsSupported` + 文件打开补丁（`AsyncLocal<UdfReader>`） | `FFProbeProvider.IsSupported` + `FfRunnerBase.OnBeforeStartProcess`（换 ffprobe、改 `-i`）+ `MediaProbeManager.GetMediaInfo` |
| **章节** | 自研 MPLS `PlayListMark` 解析 | BDInfo / MPLS | 自研 `BlurayMplsParser` |
| **远程 ISO（strm→http）** | ✅ `HttpRangeStream` + DiscUtils，只补章节 | 未见 | 未见 |
| **对用户的前置要求** | Windows 无；Linux/Docker 需自定义 ffprobe | 无（自带盘片解析） | **必须自备能读盘的 ffprobe** |

### 各自的取舍

- **PRO 最完整**：自带 `BdInfoExaminer`，不依赖 ffprobe 也能拿到 playlist 级的音轨/字幕/章节/时长，
  所以它在 Linux/Docker 上也能出结果。代价是体量大、且要靠 `AsyncLocal<UdfReader>` 把
  ISO 内部文件伪装成可读路径，侵入面广。
- **wt 最轻**：三个 patch + 一个自研 MPLS 解析，但它把"能不能读盘"整个外包给用户的 ffprobe ——
  用户配不对就等于没有这个功能。
- **本仓最省事但最挑环境**：Windows 挂载后完全交给 Emby 原生 ffprobe，信息最全、实现最简单；
  但 Linux/Docker 要用户配自定义 ffprobe，**远程 ISO 更是只补章节、没有媒体信息**。

### 本仓的一个已知浪费

`UdfTitleResolver` 其实**已经解析了 MPLS 的 STN_table**（`ParseStnTable` / `ParseStreamAttributes` /
`MapCoding` / `MapSubtitleCoding`，含音轨语言、编码、声道与字幕语言），
但 `IsoMounter.RememberChapters` 只把章节存进缓存，**音轨/字幕信息算完就丢了**，
`IsoExtraction` 也只注入 `ChapterInfo`。

也就是说：最难的解析已经有了，缺的是"把它接到探测结果上"。
补上之后，远程 ISO 与 Linux 无 ffprobe 的场景都能给出结构完整的媒体信息。

## 相关决策

- 为什么放弃 `bluray:` 协议 → [../decisions/0003-放弃-bluray-协议.md](../decisions/0003-放弃-bluray-协议.md)
- 远程 ISO 的完整实施记录 → `history/2026-10/2026-10-20-优化方案-实测版.md`
