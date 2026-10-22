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

## 相关决策

- 为什么放弃 `bluray:` 协议 → [../decisions/0003-放弃-bluray-协议.md](../decisions/0003-放弃-bluray-协议.md)
- 远程 ISO 的完整实施记录 → `history/2026-10/2026-10-20-优化方案-实测版.md`
