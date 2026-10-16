# 第 4 阶段实施方案 · ISO 截图 / 章节图（Wiki#5）

> **日期**：2026-10-16　**状态**：✅ 已实现并实机验证通过（IMG-1/IMG-2/IMG-4/IMG-5；决策 D1=a / D2=a / D3=b）
> **遗留项收尾（2026-10-16 补）**：§十一 = DV P7 FEL input-seek 花屏**定论**（ffmpeg 固有 + 量化修复方案）；§十三 = IMG-5 端到端被**「MPLS 章节未消费」上游缺口阻塞**的定论。
> **基线**：`docs/PRO功能差异全量梳理与移植计划-v8.md`（第 4 阶段）
> **上游**：第 1 阶段（ISO-1~4）✅ `1741f6d`、第 2 阶段（MPLS 语言/章节）✅ `170cd45`、第 3 阶段（DV P7）✅ `c5dc966`、ISO 挂载释放修复 ✅ `9bd8f4e`
> **调研环境**：Emby 4.10.1.0，库「混合内容」(`D:\视频\小号`)，样本 item 428 = `原盘\杜比视界测试：双层 FEL.iso.strm`（无海报，唯一会被截图触发的条目）

---

## 一、目标与验收口径

Wiki「提取 ISO 媒体信息」第 5 条原文：**ISO 截图 / 章节图**。

验收标准（对齐 PRO 可观测行为）：
1. 无主图的 ISO 条目（本地 `.iso` / strm→http ISO）能被**截图**，得到一张有效海报。
2. 抽帧进程**不再挂死**、任务不再被拖住。
3. 非 ISO 条目行为**完全不变**（零副作用）。

---

## 二、实机调研结论（Emby 4.10.1.0，全部有日志实证）

### 2.1 截图链路（实测还原）

```
LibraryApi.OrchestrateMediaInfoProcessAsync
  → 条件：enableImageCapture && !HasImage(Primary) && ImageCaptureEnabled(item, libOptions)
  → EnableImageCapture.AllowImageCaptureInstance(item)      // AsyncLocal 标记
  → _providerManager.RefreshSingleItem(item, FullRefresh)
  → VideoImageProvider.GetImage(item, Primary)
  → ImageExtractorBase.RunExtraction(...)                    // ★ 社区已挂 prefix
  → SingleImageExtractor 启动 ffmpeg 'extract-image'
```

**实测命令行**（item 428，2026-10-16 14:51:32 / 14:55:28 两次一致）：

```
ffmpeg.exe -ss 00:00:00.520  -f mpegts -threads 1
  -i "http://172.18.70.52:8090/api/strm?account=…&file_name=…FEL.iso"
  -an -sn -threads 0 -vf "thumbnail=24,format=p010,supertonemap=tonemap=hable"
  -f image2 -b:v 100M "…\cache\temp\9614266c-….jpg"
```

**结果**：ffmpeg 挂在 HTTP URL 上 **>120s 不退出**（限流代理），被人工 `taskkill` 终止；条目 428 最终仍**无海报**。

### 2.2 缺陷 A —— ISO 输入未被改写（本阶段要解决的核心）

| 形态 | 实测 | 结论 |
|---|---|---|
| **远程 strm ISO** | ffmpeg 直连 strm 代理 URL → 挂死 >120s | ❌ 完全不可用 |
| **本地 `.iso`** | ffmpeg 能按 mpegts 解码出 3840×2160 帧，但 `-ss 15` 输出 `time=00:00:00.04` | ⚠️ **无法定位**，抽到错误帧 |

> 根因：抽帧的 `-i` 直接用 `item.Path`（ISO 路径 / strm URL），**未走**第 1 阶段已有的「挂载取正片 / 头部窗口」基建。

### 2.3 缺陷 B —— 4.10 类名变更导致「截图位置」选项失效

- 4.10 的 `Emby.Server.MediaEncoding.dll` 中 **`QuickSingleImageExtractor` 出现 0 次**；实际类名为 `SingleImageExtractor` / `MultiImageExtractor`（二进制字符串实证）。
- 社区代码 `EnableImageCapture.OnInitialize` 用 `GetType("…QuickSingleImageExtractor")` → 在 4.10 上**恒为 null** → `RunExtractionPrefix` 中
  `if (ImageCaptureItem.Value != null && __instance.GetType() == _quickSingleImageExtractor)` **永不成立**。
- **实测佐证**：把 `ImageCapturePosition` 由 **10 改成 90**，ffmpeg 的 `-ss` **仍恒为 `00:00:00.520`**（Emby 自带默认值）→ 该选项在 4.10 上**完全无效**。
- 附带影响：`ImageCaptureItem.Value` 因此**永不被清空**（清空语句在该死分支内），可能长期残留标记。

---

## 三、PRO 的做法（源码实证，`_pro_tmp` = `D:\下载\AI\StrmAssistant.Releases\_pro_tmp`）

| 事实 | 证据 |
|---|---|
| PRO 把 ISO 输入写成 `bluray:"{0}"` | `strings.txt:2071` `$1$2,bluray`、`:2075` `bluray:"{0}"` |
| PRO **要求自定义 ffmpeg/ffprobe 带 `--enable-libbluray`** | `strings.txt:2419` `--enable-libbluray*`、`:2426` `Custom ffmpeg supports bluray input protocol: V`、`:2437` `…ffprobe…: X` |
| PRO 的 `CustomEncoderPath` 用途是「**Dolby Vision image extraction**」（DV 抽帧），非 ISO 专用 | `decompiled_v2/…/MediaInfoExtractOptions.cs` 的 `DescriptionL` |
| PRO 有专门的 `ExtractBlurayDisc` 处理 `.iso.strm` | `strings.txt:2045-2046` |

> **社区路线差异（已在第 1 阶段实测确认）**：Windows 下 libbluray **读不了 UDF**（文件/挂载盘/HTTP 全失败），社区已彻底放弃 `bluray:`，改用**挂载（本地）/ 头部窗口（远程）**。本阶段沿用社区既有基建，不引私有依赖。

---

## 四、实施方案（IMG-1 ~ IMG-5，零新依赖）

| 步 | 内容 | 落点 |
|---|---|---|
| **IMG-1** | `RunExtractionPrefix` 增加 `ref string inputPath`（按名匹配，兼容 4.9/4.10）；识别 ISO 输入并改写为**可读本地路径**：<br>· 本地 `.iso` → `IsoMounter.GetProbePath(isoPath)`（挂载正片，可 seek）<br>· 远程 http(s) ISO → `IsoMounter.GetRemoteProbePath(isoUrl)`（头部窗口）<br>· 取不到 → **保持原输入**（保守放行） | `Mod/EnableImageCapture.cs` |
| **IMG-2** | 修复 4.10 类名：`SingleImageExtractor` 优先，回退 `QuickSingleImageExtractor`（两者取第一个非 null）；使位置/超时覆盖在 4.10 恢复生效 | 同上 |
| **IMG-3** | 远程 ISO 的**位置策略**（见 §六 D1） | 同上 |
| **IMG-4** | 日志：命中改写/回退各一行 `Info`；异常一律 `catch` 后放行原输入 | 同上 |
| **IMG-5** | 章节图（BIF）：`ExtractVideoThumbnailTask` 走同一条 `RunExtraction` 路径，**复用 IMG-1/2 即自动覆盖**；作为验收项验证 | 验证项 |

**红线**：
- 不改 `MediaSourceInfo.Path`（只改 ffmpeg 的 `inputPath` 参数）。
- 挂载需在抽帧期间存活：`IdleTimeout`(10 min) 远大于单次抽帧耗时，天然满足；且 `GetProbePath` 每次调用会刷新 `LastUsedUtc`。
- 失败一律回退原输入，绝不影响非 ISO 条目。

---

## 五、风险与缓解

| 风险 | 说明 | 缓解 |
|---|---|---|
| `-f mpegts` 强制格式 | Emby 抽帧固定加 `-f mpegts`；挂载正片是 `.m2ts`（=mpegts）、窗口文件也是 mpegts → 兼容 | 无需改动；实测验证 |
| 远程窗口仅 8MB | 只覆盖正片开头约 1s（且该条目的 `RunTimeTicks` 也来自窗口 ≈1.04s，故 `-ss` 落在窗口内） | 见 §六 D1 |
| 位置覆盖恢复后行为变化 | IMG-2 让 `ImageCapturePosition` 在 4.10 生效，属**修复既有 bug**，但也改变了现状行为 | 保持默认值 10 不变；在验收中对比 |
| 与 `ExclusiveExtract` 共用 `RunExtraction` | 两者不冲突（不同方法/不同 prefix 目标） | 编译+日志验证 |
| 抽帧期间并发挂载 | 单条目串行，风险低 | — |

---

## 六、待决策（需明确指示后才动手）

| # | 决策点 | 选项 | 我的建议 |
|---|---|---|---|
| **D1** | 远程 ISO 的截图**位置** | a) **最小可用**：直接用现有 8MB 头部窗口（帧取自正片开头附近，`-ss` 落在窗口内即成功）<br>b) **完整**：由 UDF（头部窗口内即可解析）算出正片字节偏移+长度，用 HTTP Range 取「目标位置附近的窗口」再抽帧（可抽到 10% 处） | **a 先行**：先解决「挂死 + 完全不可用」，把 b 列为后续增强（b 依赖 UDF extent 计算，工作量明显更大） |
| **D2** | 是否一并修复**缺陷 B**（位置选项在 4.10 失效） | a) 一并修（IMG-2）<br>b) 只修 ISO 输入，位置问题另立项 | **a**：改动极小（换一个类型名查找），且不修则 `ImageCapturePosition` 对**所有**条目都无效 |
| **D3** | 本阶段范围 | a) 仅「截图」（Primary image）<br>b) 截图 + 章节图（BIF） | **b**：两者共用 `RunExtraction`，IMG-1/2 落地后章节图应自动可用，验收时一并验证即可，几乎无额外成本 |

---

## 七、验收步骤（方案批准后执行）

1. `dotnet build StrmAssistant/StrmAssistant.csproj -c Debug` 0 错误；部署 DLL（MD5 一致）。
2. 重启 Emby → 插件 Healthy、补丁数正常、0 error。
3. 开启 `EnableImageCapture`（测试期），运行「提取媒体信息」任务。
4. **本地 ISO**：日志出现 `EnableImageCapture - iso input rewritten … -> E:\BDMV\STREAM\….m2ts`；ffmpeg 正常退出（code 0）；条目获得 Primary 海报。
5. **远程 strm ISO**：日志出现改写为 `…\strmassistant_iso\iso_….iso`（窗口）；ffmpeg **不再挂死**、正常退出；条目获得海报。
6. **回归**：非 ISO 条目（如普通 strm 剧集）抽帧命令行**不含**改写日志、行为与改前一致。
7. **缺陷 B 验证**：改 `ImageCapturePosition`（如 10→90）→ 观察 ffmpeg `-ss` **随选项变化**（改前恒为默认值）。
8. 章节图（若采纳 D3=b）：对 ISO 条目触发缩略图提取，断言 BIF 生成、无挂死。
9. 测试后**还原** `EnableImageCapture` 为用户原值。

---

## 八、调研产物与还原情况

- 调研期间临时开启过 `EnableImageCapture=true`、`ImageCapturePosition=90`，**已还原**为用户原值（`false` / `10`）。
- 调研期间产生的挂死 ffmpeg 进程**已终止**；无残留临时文件（远程窗口临时文件由插件 TTL 自动清理）。
- 未产生任何条目状态变更（item 428 仍为无海报，与调研前一致）。

---

## 九、实施记录（2026-10-16，决策 D1=a / D2=a / D3=b）

落点全部在 `StrmAssistant/Mod/EnableImageCapture.cs`，**零新依赖**，仅改 1 个文件。

| 步 | 内容 | 关键实现 |
|---|---|---|
| **IMG-1** | ISO 抽帧输入改写 | `RunExtractionPrefix` 新增 `ref string inputPath`；新增 `RewriteIsoInput(ref inputPath)`：<br>· 远程 http(s) ISO → `IsoMounter.GetRemoteProbePath`（8MB 头部窗口临时文件）<br>· 本地 `.iso` → `IsoMounter.GetProbePath`（挂载正片 m2ts，可 seek）<br>· 取不到 → 原样放行；全程 `try/catch` |
| **IMG-2** | 4.10 类名修复 | 字段 `_quickSingleImageExtractor` → `_singleImageExtractor`；初始化改为 `GetType("SingleImageExtractor") ?? GetType("QuickSingleImageExtractor")`（新名优先、旧名回退），找不到时 `Warn`。位置/超时覆盖的判定改用它 |
| **IMG-4** | 日志与容错 | 命中改写/无法改写各一行 `Info`（URL 剥 query，避免泄露 account/pickcode）；异常一律 `Debug` 后放行原输入 |
| **IMG-5** | 章节图（BIF）覆盖 | 无需改代码：`ImageSeriesExtractor` / `QuickImageSeriesExtractor` **均未覆盖 `RunExtraction`** → 直接命中已挂 prefix（静态证明，见 §十） |

**红线遵守情况**：只改 ffmpeg 的 `inputPath` 参数，**未改** `MediaSourceInfo.Path`；`RunExtraction` 的 4.9/4.10 兼容策略保持不变（只声明两版本类型一致的 `__instance` / `inputPath` / `startOffset`）。

---

## 十、实机验证结果（Emby 4.10.1.0，全部有日志实证）

| # | 验收项 | 期望 | 实测 | 结论 |
|---|---|---|---|---|
| 1 | 编译 | 0 错误 | 0 错误 / 1 既有 warning；MD5 一致部署 | ✅ |
| 2 | 重启加载 | Healthy、补丁数正常、0 error | `All 36 patches initialized` / `Healthy` / 0 error；`✓ EnableImageCapture Status=Applied` | ✅ |
| 3 | **远程 ISO（IMG-1）** | 改写为窗口文件、不挂死、出海报 | `EnableImageCapture - iso input rewritten (remote window) … -> C:\…\iso_3f201fec….iso`；ffmpeg `-i` 为本地窗口、**2.3s 退出**（原 >120s 挂死）；item 428 获 Primary 海报（画面干净） | ✅ |
| 4 | **本地 ISO（IMG-1）** | 改写为挂载正片、出海报 | `iso input rewritten (mounted title) …FEL2.iso -> E:\BDMV\STREAM\00000.m2ts`；`-i file:"E:\…\00000.m2ts"`、**2.8s 退出**；item 424 获海报 | ✅ |
| 5 | **回归（非 ISO）** | 无改写日志、行为不变 | item 79 / 429 处理时**无任何改写日志**、无 error；海报由在线元数据源正常供给 | ✅ |
| 6 | **IMG-2（位置选项）** | `-ss` 随 `ImageCapturePosition` 变化 | 10 → `-ss 00:00:00.520`；90 → **`-ss 00:00:00.937`**（=1.0417s×0.9，与公式吻合） | ✅ |
| 7 | **IMG-5（章节图）** | 走同一 `RunExtraction` | 静态证明：`ExtractVideoImagesOnInterval` → `ImageSeriesExtractor`/`QuickImageSeriesExtractor`（**无 `RunExtraction` override**）→ 基类（已挂 prefix）。**实测受限**：库 `EnableChapterImageExtraction=False`，BIF 任务取项为 0，未做端到端实测 | ⚠️ 静态✅ / 实测受限 |

**补充结论**：
- `-f mpegts`（来自 `GetInputFormatArgument(container=MpegTs)`）对窗口文件与挂载 m2ts **均兼容**（实测抽帧成功）。
- 改写后 `protocol` 仍为 `Http`，但 item 428 的 `RequiredHttpHeaders={}` → `GetProbeInputArgs` 不追加 `-headers`，**无副作用**。
- Emby 自带 `GetThumbnailPositionTicks` = `max(runtime*0.1, min(runtime*0.5, 20s))`，与插件公式**完全一致**（仅百分比写死 10%）→ IMG-2 在默认值 10% 下**行为不变**，仅在改动选项时生效。

---

## 十一、遗留项 B 定论：DV P7 FEL 盘「非零 input seek」抽帧花屏（2026-10-16 补充实证）

> 状态：**根因已定位、方案已量化**。结论：**ffmpeg 固有行为，非插件缺陷**；已找到低风险修复路径，待决策。

### 11.1 复现与对照（独立 ffmpeg，脱离插件）

测试素材：`杜比视界测试：双层 FEL2.iso`（本地，挂载为 `E:`）→ `E:\BDMV\STREAM\00000.m2ts`。
ffmpeg：`embyserver-win-x64-4.10.1.0\system\ffmpeg.exe`（5.1-emby_2023_06_25_p6）。
流结构：`index0 = hevc 3840×2160 HDR10`（BL）、`index1 = hevc 1920×1080 DolbyVision Profile 7.6`（EL/FEL）；时长 **30.667s**；关键帧 **35 个、间隔 ≈1.0s**（PTS 基址 4200s）。
抽帧位置 `15.333s` = `max(rt×0.1, min(rt×0.5, 20s))`（rt=30.667）→ 与 Emby 自带 `GetThumbnailPositionTicks` 公式吻合。

| # | 命令形态 | 结果 | 耗时 |
|---|---|---|---|
| T1 | `-ss 15.333` **在 `-i` 前**（input seek，Emby 现状） | **灰底 + 块状伪影**（80352 B） | 0.66s |
| T2 | `-ss 15.333` **在 `-i` 后**（output seek） | **干净真实帧** | **26.7s** |
| T3 | `-ss 0`（input） | 干净；与 T4 **字节完全相同**（80352→137359，`-ss 0` 被 ffmpeg 优化为空操作） | 0.43s |
| T4 | 不 seek | 干净 | 0.44s |
| T5 | input seek 后取连续 5 帧 | 第 1~6 帧损坏，**第 7 帧起恢复** | — |
| T6 | input seek + `-map 0:0`（仅 BL） | **同样损坏** → 与 EL/FEL 层无关 | — |
| T7 | input seek + `-map 0:1`（仅 EL） | 非伪影 | — |
| T10 | input seek + `-accurate_seek` | **同样损坏** → 该开关无效 | — |
| T14 | input seek + `-f mpegts` | **同样损坏** → 与 `-f mpegts` 无关 | — |
| T11/T15 | input seek + **`-skip_frame nokey`** | **干净真实帧** | **0.38s** |
| T16 | `-skip_frame nokey` @5s | 干净 | 0.30s |
| T18 | `-skip_frame nokey` @29.9s | 有帧 | — |
| T18b | `-skip_frame nokey` @30.5s / @30.6s | **无帧** | — |
| T19 | **普通 input seek** @30.5s | **同样无帧** | — |

**根因**：mpegts（BDMV/ISO 的 m2ts）无索引，ffmpeg input seek 按字节估算落点，**落在非 IDR 帧附近** → 解码器需重同步，seek 点后最初若干帧（本样本约 0.3~0.5s / 6 帧）解码损坏。**与插件、`-f mpegts`、DV FEL/EL 层、`-accurate_seek` 均无关**（逐项对照排除）。

### 11.2 关键：Emby 真实链路下同样花屏（不是理论问题）

Emby 抽帧实际使用 `-vf "thumbnail=24,format=p010,supertonemap=tonemap=hable"`。实测：

| 形态 | 画面 | 耗时 |
|---|---|---|
| 现状（input seek + `thumbnail=24`） | **仍花屏**（窗口内全损坏，`thumbnail` 只能从损坏帧里挑） | 2.65s |
| `-skip_frame nokey` + `thumbnail=24` | **干净** | 6.31s |
| output seek + `thumbnail=24` | 干净 | 28.9s |

→ **本地 ISO 的海报会真的变花屏**，属用户可见缺陷。

### 11.3 候选方案量化

| 方案 | 画面 | 速度（本样本） | 落点 | 风险 | 结论 |
|---|---|---|---|---|---|
| 维持 input seek（现状） | 损坏 | 2.65s | 精确 | 已存在 | 缺陷 |
| output seek | 干净 | **28.9s**（15s 素材） | 精确 | 真实影片解码时长 ≈ 抽帧位置，**必超 Emby 5min 超时** | ❌ 不可行 |
| **`-skip_frame nokey`** | **干净** | 6.31s | **≤1 GOP（本样本 1.0s）** | 需在 `-i` **前**注入输入参数；影响 `GetExtraInputArguments` 共享路径；与 `thumbnail=24` 有交互（帧数变多） | ✅ 可行 |

**边界安全性**：普通 input seek 在 30.5s 处**同样取不到帧**（文件仅 30.667s）→ `-skip_frame nokey` **不引入新的失败面**（实测 T18b 与 T19 一致）。

### 11.4 建议

**建议修**，但必须**限定作用域**：`-skip_frame nokey` 只对「由 ISO 改写而来」的输入注入（判定点已有：`IsoMounter` 的挂载根 / 远程窗口临时目录），避免改变所有常规条目的抽帧行为。
落点代价 ≤1 个关键帧间隔（典型 Blu-ray ≈1s），对海报无感知。

---

## 十二、测试环境还原

- 配置已还原：`EnableImageCapture=false`、`ImageCapturePosition=10`（`EnableIsoExtraction=true` 未动）。
- 测试期间改动的条目海报已还原：item 424（本地 ISO，已回写原始干净帧）、item 79（已回写原图）、item 429（图片 hash 与原值一致，内容未变）。
- item 428 **新增** Primary 海报并保留（该条目原无海报，正是本阶段修复的预期结果）。
- 挂载已自动释放（`E:` 未挂载、挂载清单已删）；远程窗口临时文件已清理。

---

## 十三、遗留项 A 定论：IMG-5 端到端被「MPLS 章节未消费」阻塞（2026-10-16 补充实证）

> 状态：**已定位为上游缺口，非本阶段缺陷**。IMG-5 端到端验证**在缺口关闭前无法进行**。

### 13.1 现象

`ExtractVideoThumbnailTask` 取项条件之一为 `HasChapterImages = false`，随后 `RefreshThumbnailImages` 用 `_itemRepository.GetChapters(item)` 取章节；**章节为空时直接 `return false`**（`VideoThumbnailApi.cs:198-205`）→ 不产生任何 BIF。

实测两个 ISO 条目**均无章节**：

| 来源 | 证据 |
|---|---|
| Emby DB | `library.db` → `Chapters3`：**item 424 / 428 无任何记录**（其余 80+ 条目均有，10 章或 16 章） |
| 插件持久化 | `D:\视频\小号\杜比视界测试：双层 FEL2-mediainfo.json` → `"Chapters":[]` |

### 13.2 根因：第 2 阶段只做了「解析」，没做「消费」

该测试盘的 `BDMV\PLAYLIST\00000.mpls` **确实带 1 个 PlayListMark**（实测解析：`markType=1` entry mark、`timestamp=0x0B43E940 /45000 = 4200s` = 标题起点 0s、`numMarks=1`）。

但全仓核对：

- `TitleInfo.Chapters` 的**唯一赋值点**在解析器内部（`UdfTitleResolver.cs:563 entry.Chapters = marks`）；
- **没有任何读取点** —— `IsoMounter.ResolveMountedTitle`（`:429`）/ `ResolveInProcessTitle`（`:462`）只取 `RelativePath` / `SourceName` / `DurationSeconds` / `IsBluray`，`Chapters`（及 `AudioStreams`/`SubtitleStreams`）**解析后即丢弃**；
- 全仓 `.Chapters =` 赋值仅此一处；所有 `SaveChapters` 调用均来自通用链路（`ChapterApi` / `MediaInfoApi` 持久化-还原 / `VideoThumbnailApi` 合并回写），无一来源于 MPLS 解析结果；
- 第 2 阶段提交 `1741f6d`（`feat(iso): 远程 ISO 探测 + MPLS 语言/章节解析`）的 diff 亦只新增解析，未新增消费。

### 13.3 后果与建议

- **后果**：ISO 条目永远没有章节 → **ISO 的章节图/BIF 永远不会生成**；`TitleInfo` 里解析出的音轨/字幕语言同样未被使用。
- **对第 4 阶段的影响**：IMG-5 的「端到端」验收**被此缺口阻塞**（不是配置问题：本环境库 `EnableChapterImageExtraction=False`，且即便开启，ISO 条目仍无章节可取）。静态证明（§十 #7）依然成立且已足够。
- **建议**：先补齐「把 MPLS 章节写入条目」的消费链路（属**第 2 阶段收尾**，价值高于第 5 阶段的 G2/G4），再回头做 IMG-5 端到端。

---

