# 0003 · 放弃 ffprobe 的 bluray: 协议读 ISO

- **状态**：❌ 已放弃
- **日期**：2026-10（ISO 专项排查期间）
- **相关**：[../architecture/iso.md](../architecture/iso.md)

## 背景

Emby 内置的 `ffprobe.exe`（5.1-emby）编译时带了 `--enable-libbluray`，看起来可以直接
`ffprobe bluray:D:\path\to\disc.iso` 读到正片和章节 —— 如果可行，ISO 支持会非常轻量。

## 决策

**放弃这条路。Windows 上改用"系统挂载 → 盘符路径"；Linux 上改用插件内置 DiscUtils.Udf 解析。**

## 理由

`libbluray` 在 Windows 上读不了 UDF。`.iso` 是 UDF 文件系统，libbluray 依赖 OS 提供的
UDF/光盘挂载能力，Windows 没有对应实现，所以 `bluray:` 协议在 Windows 直接失败。

Linux 上理论可行，但那样等于 Windows / Linux 走两套完全不同的代码，维护成本高于收益。

## 后果

- Windows：`IsoMounter` 调 PowerShell `Mount-DiskImage` 拿盘符，之后当普通文件路径处理，
  Emby 原生 ffprobe 就能读。**这条路径之所以"能读"，是因为挂载由外部子进程完成，Emby 自己不碰 iso 文件。**
- Linux/Docker：进程内 `DiscUtils.Udf` 解析，需要配合自定义 `CustomProbePath` 指向支持 UDF 的 ffprobe。
- 顺带确认：ffprobe **直读裸 `.iso`（本地或 http）可以当 mpegts 解析**，但那样拿到的是
  "整个盘当成一条流"，没有正片选择和章节，不满足需求。

## 后续

远程 ISO（strm 里写 http URL）走的是另一条路：`HttpRangeStream` + DiscUtils.Udf，
只拉 UDF 元数据区。这条已实现，见 [../architecture/iso.md](../architecture/iso.md)。
