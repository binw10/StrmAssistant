# PRO 更新日志对照 × 项目现状 × 移植优化方案（v9）

> **基准**：PRO `StrmAssistantPro.dll` **3.0.0.55+3abcae8**（`BuildTimeUtc 2026-10-15T03:46:09Z`，`netcoreapp6.0` / MonoMod）
> **对照方**：社区版 `D:\下载\AI\StrmAssistant-myp015`（`net6.0` / `Lib.Harmony 2.4.2`，`AssemblyVersion 3.0.55`）
> **对照素材**：① GitHub Releases 正文全文（v3.0.0.45 → v3.0.0.55）② 双版本反编译树 `_pro_tmp/decompiled`(3.0.0.53) 与 `decompiled_v2`(3.0.0.55) ③ 项目源码 + 631 条提交历史
> **日期**：2026-10-17　**状态**：取代 v8（`PRO功能差异全量梳理与移植计划-v8.md`）作为当前基线；v8 的 ISO 四阶段已全部落地，本文件补 v8 未覆盖的 **3.0.0.53→.55 增量**与**新发现缺口**

---

## 〇、一页看懂

| 层面 | 结论 |
|---|---|
| **PRO 更新日志** | ⚠️ **PRO 没有 changelog 文件**（本地全量搜 `changelog/releasenote/更新日志` = 0 命中）。唯一权威来源是 **GitHub Releases 正文**，已抓全 v3.0.0.45→.55 |
| **.53 → .55 增量** | 仅 **6 条**（3 优化 + 3 修复），其中 **3 条已在社区版落地 / 1 条部分 / 2 条未移植** |
| **⚠️ 方法论** | PRO **逐构建重新混淆**（Unicode 类名 + 控制流平坦化 + 字符串加密）→ **源码行级 diff 全是噪声**（实测 32 行"差异"100% 是重命名）。本报告一律走**语义锚点比对**（属性名/枚举名/资源键/路由——这些不混淆） |
| **本轮新发现真缺口** | **3 项**（`MediaInfoExtended`、豆瓣客串演员、搜索索引「优化」）+ v8 遗留 **4 项**（G1~G4，全 P3）= **7 项** |
| **可立即做（半天内）** | **2 项**：豆瓣客串演员一行修复 + 多版本 `MediaInfoExtended` 枚举补全 |
| **社区版反超 PRO** | **93 个独有属性 + 39 个 Mod 补丁 + 11 个 Core 类**——不要为"对齐"误删 |
| **v8 计划被推翻的 1 项** | ISO-2 的 `bluray:"{0}"` 协议**实测失败**（Windows libbluray 读不了 UDF），社区改走 **Windows 原生挂载**并已成功 |

---

## 一、PRO 更新日志从哪来 + 方法论红线

### 1.1 PRO 确实没有 changelog 文件

对 `_pro_tmp/strings.txt`（123 KB，从 3.0.0.55 程序集 dump）做全量检索：

```
grep -iE "changelog|releasenote|release_note|更新日志|版本说明" strings.txt   → 0 命中
```

程序集里与"更新"相关的字符串只有：

```
https://api.github.com/repos/sjtuross/StrmAssistant.Releases/releases/latest
https://raw.githubusercontent.com/sjtuross/StrmAssistant.Releases/main/latest.json
https://raw.githubusercontent.com/sjtuross/StrmAssistant.Releases/main/manifest_v2.json
```

→ 即 **PRO 的 `UpdatePluginTask` 自己也是去 GitHub Releases 取更新说明**。因此 **GitHub Releases 正文 = PRO 官方更新日志**，这是唯一权威来源。

### 1.2 已抓取的官方更新日志（v3.0.0.45 → v3.0.0.55）

| 版本 | 发布日期 | 要点 |
|---|---|---|
| **v3.0.0.55** | 2026-10-15 | ①中文搜索增强-特殊符号处理 ②仪表盘支持搜索索引**重建** ③多版本美化**新增一种显示格式** ④适配 MovieDb **1.9.9.0** ⑤修豆瓣中文化**未处理客串演员** ⑥其它修复与性能 |
| v3.0.0.54 | — | **未发 release**（manifest 有哈希，无 release 页） |
| v3.0.0.53 | 2026-09-27 | ffprobe/ffmpeg 升 9.0.2；**DVD ISO 提取+截图**；提升自动识别 http iso；多版本美化/排序适配更多客户端；**HDR Vivid 检测**；跨刮削器中文海报排序；仪表盘**展示+优化**搜索索引；开放自动刮削片头片尾开关；修混合库豆瓣图标 |
| v3.0.0.52 | 2026-09-12 | 分组通知模板统一 |
| v3.0.0.51 | 2026-08-30 | 剧集刷新升级为**元数据刷新**；演员页提速；本地 tmdb **合并补丁**；插件更新检测避免 403；隐藏项 `ImageExtractionTimeoutMultiplier` |
| v3.0.0.50 | 2026-08-05 | 备选语言日文回退；隐藏项 `ClearEmbeddedMetadata`；默认不排除 ts/m2ts；适配 MovieDb 1.9.2.0 |
| v3.0.0.49 | 2026-07-13 | 仪表盘活动任务/会话可取消；内存超限倒计时；ffprobe 8.1.2；**`.iso.strm` 命名标准**；**http ISO 截图/章节图** |
| v3.0.0.48 | 2026-06-10 | **http 链接 ISO 媒体信息提取**；计划任务多语言；电视总集数含特别篇 |
| v3.0.0.47 | 2026-06-03 | 刮削 Tidb 最长片头；修媒体库封面 |
| v3.0.0.46 | 2026-05-28 | 仪表盘页；内存联动重启；分布式声纹提取；搜索页豆瓣评分/媒体信息 |
| v3.0.0.45 | 2026-05-04 | 片头片尾刮削器支持电影/优先级+apikey；适配 MovieDb 1.9.1.0 |

> **关键观察**：`.48/.49` 的 "http ISO" 系列（`.iso.strm` 命名标准 → http 提取 → http 截图/章节图）是 PRO 用 **3 个版本**才铺完的；社区版第 4 阶段一次性打通了**本地 ISO**，HTTP 形态仍是缺口（见 §四 P1）。

### 1.3 ⚠️ 方法论红线：PRO 源码**不能**做行级 diff

PRO 每次构建对**所有私有符号重新随机命名**（Unicode 转义名）+ 控制流平坦化 + 字符串加密。实测证据：

```
diff decompiled/StrmAssistant.Options/IgnoreOnPersistAttribute.cs
     decompiled_v2/StrmAssistant/Options/IgnoreOnPersistAttribute.cs
→ 报 32 行"差异"（文件共 64 行）
→ 逐行核对：100% 是 A_0093_008B_0096_… 这类随机名互换，语义零变化
```

同样地，`UpdatePluginTask.cs` 行级 diff 报 **7266 行差异**（文件仅 4363 行）——纯噪声，不代表任何功能变更。

**因此本报告一律采用「语义锚点比对」**，只比不混淆的标识符：

| 锚点 | 为什么可靠 | 例子 |
|---|---|---|
| **Options 属性名** | 是持久化 JSON 的键，改了就丢配置 | `ImageCapturePostProcessMode` |
| **枚举成员名** | 同上，且被 UI 下拉引用 | `MediaInfoExtended` |
| **资源键** | `[DescriptionL("Xxx_Yyy")]` 三语 resx 的键 | `MultiVersionDisplayOption_MediaInfoExtended_MediaInfoExtended` |
| **路由** | `[Route("/...")]` 前端硬编码 | `/modules/filtermenu/filtermenu.template.html` |
| **AssemblyInfo** | 版本/构建时间，唯一的"真"差异 | `3.0.0.53+2222f8a` → `3.0.0.55+3abcae8` |

**该方法已被反证有效**：在 `.53` vs `.55` 上精确命中了 `MediaInfoExtended` 这个**唯一新增枚举成员**（§2.3）。

---

## 二、PRO 3.0.0.53 → 3.0.0.55 逐条对照（6 条）

> 版本间真实差异：`AssemblyInfo` 显示构建时间 `2026-09-27` → `2026-10-15`（相隔 18 天）；文件层面 **PRO .55 删除了 `Provider/MovieDbSeriesProvider.cs`**（社区版仍有 → 社区**多**不是缺）。

### 2.1 总表

| # | PRO .55 更新点 | PRO 源码实证 | 社区版现状 | 判定 | 工作量 |
|---|---|---|---|---|---|
| **S1** | 中文搜索增强 — 优化各种特殊符号的处理逻辑 | 符号处理在加密/混淆段内，**无法逐字还原** | `EnhanceChineseSearch.NormalizeQueryTerm`(:103) 已剥离 `·` `-`；索引侧(:89) 额外剥离 `'` `.` | 🟡 **部分**（置信中） | 小 |
| **S2** | 仪表盘支持搜索索引**重建** | `RebuildSearchIndex()` @`A----------/A----------.cs:24402` | **已移植**：`HomePageView.cs:169 case "ScheduleIndexRebuild"` → `EnhanceChineseSearch.ScheduleIndexRebuild()`(:60) + 标记文件 + 重启重建 | ✅ **已移植** | — |
| **S3** | 多版本美化**新增一种显示格式** | `MultiVersionDisplayOption` 枚举**新增 `MediaInfoExtended`**（见 §2.3，`.53` 无 / `.55` 有） | 社区 `MultiVersionDisplayOption`(:102) 只有 3 值 | 🔴 **未移植** | 中 |
| **S4** | 适配 MovieDb **1.9.9.0** | PRO 硬绑定反射目标 | 社区走**反射+缺失诊断**（`MetadataEnhanceOptions.cs:390-415` + `ChineseMovieDb.cs:90-110`），天然容错；仅**未做 1.9.9.0 实机验证** | 🟡 **部分等价** | 小 |
| **S5** | 豆瓣中文化**未处理客串演员** | PRO 修了该 bug | 社区 `DoubanAssist.cs:635` **只处理 `PersonType.Actor`**——**同源 bug** | 🔴 **未移植**（一行级） | 极小 |
| **S6** | 其它已知问题修复和性能优化 | 不可枚举 | — | ⚪ **不可移植** | — |

### 2.2 S1 详述：中文搜索特殊符号

社区现状（`StrmAssistant/Mod/EnhanceChineseSearch.cs`）：

```csharp
// 索引侧 :89
.Replace("'", "").Replace(".", "").Replace("·", "").Replace("-", "")
// 查询侧 :103 NormalizeQueryTerm
.Replace("·", "").Replace("-", "")          // 句点/引号刻意保持原查询行为
```

PRO .55 的符号处理位于**加密字符串段**内，`grep -E 'Replace\("[^A-Za-z0-9]'` 在 `decompiled_v2/` 中 **0 命中** → **无法逐字还原其符号集**。

**结论**：社区版已有 `·`/`-`/`'`/`.` 四类处理，**方向一致、覆盖可能不全**。建议**不追 PRO 的实现**，改为**按语料驱动补符号**（见 §六 阶段 0-B），成本低且收益可验证。

### 2.3 S3 详述：`MediaInfoExtended` —— 本报告最硬的发现

**证据链（三个独立锚点全部命中）：**

**① 枚举成员（`.53` 无 / `.55` 有）**

```csharp
// _pro_tmp/decompiled/…/MultipleVersionsOptions.cs   (3.0.0.53)
public enum MultiVersionDisplayOption {
    SystemDefault, MediaInfo, FileNameWithoutExtension
}

// _pro_tmp/decompiled_v2/…/MultipleVersionsOptions.cs (3.0.0.55)
public enum MultiVersionDisplayOption {
    SystemDefault, MediaInfo,
    MediaInfoExtended,        // ← .55 新增
    FileNameWithoutExtension
}
```

**② 资源键**：`.55` 的 `strings.txt` 与 `Resources.cs` 均含
`MultiVersionDisplayOption_MediaInfoExtended_MediaInfoExtended`（`.53` 树中不存在）。

**③ 渲染分支**（`decompiled_v2/A----------/A----------.cs` 反平坦化后）：

```
case MediaInfoExtended:  goto IL_0229
case MediaInfo:          goto IL_02c5

IL_02c5 (MediaInfo)          : text  = Format(val.MediaStreams, val.Bitrate)
IL_0229 (MediaInfoExtended)  : text2 = Format(val.MediaStreams, val.Bitrate, val.Size)
```

→ **`MediaInfoExtended` = `MediaInfo` + 文件体积（Size）**。

**社区现状**（`StrmAssistant/Web/Service/MultiVersionService.cs:117`）：

```csharp
var label = uiOptions.MultiVersionDisplayOption switch
{
    "FileNameWithoutExtension" => Path.GetFileNameWithoutExtension(path),
    _ => GetQualityLabel(video?.Height)   // MediaInfo（默认）—— 仅按高度出 8K/4K/1440p/1080p/720p/SD
};
```

⚠️ **社区版连 PRO 的 `MediaInfo` 都是简化版**（只有分辨率档，没有编码/码率）；`MediaInfoExtended` 需要先补一个**服务端媒体信息串构造器**。这是 S3 工作量"中"而非"小"的原因。

### 2.4 S5 详述：豆瓣客串演员 —— 社区版存在**同源 bug**

```csharp
// StrmAssistant/Provider/Douban/DoubanAssist.cs:635
foreach (var person in people.Where(p =>
             p.Type == PersonType.Actor && !string.IsNullOrWhiteSpace(p.Name)))
```

社区版**只遍历 `Actor`**，`PersonType.GuestStar`（客串）全部漏掉 —— 与 PRO .55 修的正是**同一个 bug**。

已核对 Emby 4.9.1.90 的 `MediaBrowser.Model.Entities.PersonType`（`ilspycmd` 实证）：

```csharp
public enum PersonType { Actor, Director, Writer, Producer, GuestStar, Composer, Conductor, Lyricist }
```

→ **是普通枚举（非 flags）**，必须用 `||` 显式列出：

```csharp
p.Type == PersonType.Actor || p.Type == PersonType.GuestStar
```

配套：`CelebrityRoleRegex`(:236) 的角色词从"仅 `饰[:：]`"扩到 `饰|客串|嘉宾`。

---

## 三、全量能力面逐层对照

> 方法：对两版 **Options 目录**做机器比对（只取不混淆的 `public` 属性名 + 枚举成员名）。

### 3.1 Options 层（机器比对结果）

```
PRO Options 属性 = 212      社区 Options 属性 = 238
PRO 有 / 社区无 = 67        社区有 / PRO 无 = 93
```

**67 项"PRO 有 / 社区无"的三分类**（逐项人工核对）：

| 分类 | 数量 | 说明 |
|---|---|---|
| **A. 真功能缺口** | **7** | 见 §四（v8 的 G1~G4 + 本轮新发现 S3/S5/S2-残余） |
| **B. 命名/形态差异（语义等价）** | **44** | 社区用 bool/单值枚举/平铺实现同一语义，例：`MultiVersionDisplayPreference`↔`MultiVersionDisplayOption`、`EnableDoubanAssistScraping`↔`EnableDoubanAssist`、`MetadataRefresh*`↔`EpisodeRefresh*`、`PersonRefreshOption*`↔`RefreshPersonOption*`、`SearchTuningOption*`↔`SearchItemTypeList`+4 个 bool、`Is*PluginSupported`↔`Is*PluginLoaded` |
| **C. 明确不移植** | **16** | 授权（`LicenseText`/`LicenseWarningStatus`/`MITLicenseText`/`ShowLicenseOptions`/`ShowLicenseWarning`/`ShowDisclaimerButton`/`Part1`/`Part2`）、PRO 内部态（`LastProxyCheckSucceeded`/`SuppressPluginUpdates`/`ShowMetadataExtraOptions`）、已被社区更优实现取代（`MemoryOptions`/`MemoryUsageLimitMB`/`SystemInfoItemList`）、框架差异（`FileSaved`/`FileSaving`/`Options`/`OptionsFileName`/`PluginId`）、UI 按钮（`DoubanScrapeButton`/`DoubanScrapeProgress`） |

### 3.2 社区有 / PRO 无（93 项）—— 社区**反超面**，禁止误删

| 模块 | 社区独有属性/实现 |
|---|---|
| **影音直链 `DirectLinkOptions`** | `EnableStrmDirectRedirect`/`StrmDirectRedirectFollow302`/`StrmVideoDirectRedirectClientBlacklist`/`StrmAudioDirectRedirectClientBlacklist`/`EnableLiveTvDirectRedirect` —— **PRO 完全没有** |
| **内存运维** | `EnableMemoryAutoRestart`/`MemoryAutoRestartThresholdMB`/`EnableMemoryCleanup`/`MemoryCleanupIntervalMinutes`/`CleanMemoryNowButton`/`RuntimeStatusList` |
| **豆瓣细分** | `DoubanCastChinese`/`DoubanPosterFallback`/`DoubanRatingOverride`/`DefaultNewLibraryTmdb` |
| **ISO 用户开关** | `EnableIsoExtraction`（PRO 内建无开关） |
| **搜索** | `BlockSearchRecommend`/`ExcludeOriginalTitleFromSearch`/`MixedChineseVariants`/`DigitsAsTmdbId`/`IsChineseSearchSupported` |
| **抓取代理** | `ScrapeProxyWhitelist`/`ShowScrapeProxyWhitelist`/`ProxyRoutingMode` |
| **元数据缓存** | `IsMetadataCacheEnabled`/`IsMetadataCacheReadEnabled`/`IsMetadataCacheWriteEnabled`/`MetadataCacheModeList` |
| **MovieDb 版本诊断** | `MovieDbPluginVersion`/`MovieDbVersion`/`MovieDbVersionTier`/`IsMovieDbVersionCompatible` |
| **UI 框架层** | `Caption`/`SubCaption`/`ShowBack`/`ShowSave`/`OKButtonCaption`/`PrimaryDialogAction`/`WizardHidingBehavior` 等 |

### 3.3 ScheduledTask 层

| PRO（14） | 社区（11） | 差异 |
|---|---|---|
| `BuildDoubanCacheTask`/`DeletePersonTask`/`ExtractIntroFingerprintTask`/`ExtractMediaInfoTask`/`ExtractVideoThumbnailTask`/`MergeMultiVersionTask`/`PersistMediaInfoTask`/`RefreshPersonTask` | 同名 | ✅ 齐 |
| `RefreshMetadataTask` | `RefreshEpisodeTask` | 命名不同，功能同 |
| `ScanExternalTrackTask` | `ScanExternalSubtitleTask` | PRO 覆盖更多轨道类型，社区聚焦字幕 |
| `UnifiedIntroDbProvider`(Provider) | `ScrapeIntroDbTask` | 实现形态不同，效果等价 |
| `RestartServerTask` | `EnableMemoryAutoRestart` | 社区更优 |
| `TriggerMergeMovieTask`/`TriggerPopulateDoubanIdTask` | 合并进主任务 | 等价 |
| `UpdatePluginTask` | **已主动删除**（`170cd45`） | 刻意决策，不移植 |

> ✅ **ScheduledTask 层无真缺口。**

### 3.4 Web / Provider 层

| 项 | PRO | 社区 | 判定 |
|---|---|---|---|
| JS 注入 | 拆 11 端点（`GetItemJs`/`GetListJs`/`GetMediaInfoJs`/`GetIndicatorJs`/`GetRefreshDialogJs`/`GetLibraryOptionsEditorJs`/`GetLinkedItemsJs`/`GetListControllerJs`/`GetShortcutJs`/`GetStrmAssistantJs`/`GetFilterMenuHtml`） | 聚合 `GetStrmAssistantJs` + `Web/Helper/ShortcutMenuHelper.cs`（token 替换式） | ✅ 等价（社区聚合，PRO 按需） |
| 清理类 API | `ClearChapterImage`/`ClearIntro`/`ClearMediaInfo`/`DeleteVersion`/`DeletePerson`/`LockItem`/`CopyVirtualFolder` | 同名 | ✅ 齐 |
| **筛选菜单** | `GetFilterMenuHtml` → 覆盖 `/modules/filtermenu/filtermenu.template.html`，注入 **HasDoubanId / HasImdbId 筛选** | **无**（`grep HasDoubanId` 全项目 0 命中） | 🔴 **缺口**（见 §四 P2） |
| 分布式写回 | `SyncMediaInfo` | 无 | ⚪ 高风险，暂缓 |
| 社区独有 API | — | `GetDoubanRatings`/`GetMultiVersionInfo` | 社区反超 |
| Provider | `BangumiExternalId`/`DoubanExternalId`/`IntroMarkerProtector`/`MovieDbEpisodeGroupExternalId`/`PersistMediaInfoProvider`/`UnifiedIntroDbProvider`（`.55` 已删 `MovieDbSeriesProvider`） | `MovieDbEpisodeGroupExternalId`/`MovieDbSeriesProvider`/`Douban/DoubanAssist` | ✅ 等价 |

### 3.5 Mod 层 —— 社区最大领先面

| 项 | 数值 |
|---|---|
| 社区 `Mod/` 文件数 | **39** |
| PRO 对应文件数 | **0**（PRO 全塞进 MonoMod IL 补丁） |
| 社区 `Core/` 适配类 | **11**（`EmbyVersionAdapter`/`FastReflection`/`HealthCheck`/`MemoryGuard`/`PerformanceMonitor`/`ConflictDetector`…） |

> ⚠️ **整个 `Mod/` + `Core/` 层是社区自研**，PRO 无对应文件。任何"对齐 PRO"的改造**不得删除**这两层。

---

## 四、值得移植清单（按价值密度排序）

| 优先级 | 项 | 来源 | 价值 | 工作量 | 风险 |
|---|---|---|---|---|---|
| **P0-A** | **豆瓣客串演员中文化**（`Actor` → `Actor \| GuestStar`） | PRO .55 S5 | 社区存在**同源 bug**，豆瓣重度用户直接可见 | **极小**（1 处过滤 + 1 处角色词） | 极低 |
| **P0-B** | **多版本 `MediaInfoExtended` 显示格式** | PRO .55 S3 | 多版本原盘用户一眼看出"哪个是 4K 还是 1080p、多大体积" | **中**（枚举+3 处 resx+JS/服务端标签构造器） | 低 |
| **P1** | **HTTP/strm ISO 全链路**（`.iso.strm` → 提取/截图/章节） | PRO .48/.49 | **你的主场景**（v8 §3.2 实测：样本 `Container=None`，从未被探测） | **大** | 中（守"不改 `MediaSourceInfo.Path`"红线） |
| **P2** | **筛选菜单 HasDoubanId / HasImdbId** | PRO `GetFilterMenuHtml` | 库内按"有无豆瓣/IMDb 匹配"筛选，补齐元数据时高效 | 中 | 低（只覆盖模板资源，不动数据） |
| **P2** | **搜索索引「优化」**（`OptimizeSearchIndex` 回收页/字节） | PRO .53 | 社区已有"重建"，缺"空间回收"；仪表盘可见收益 | 小 | 低（只读 `PRAGMA` + 结果展示） |
| **P3** | **`CatchupMinimumDelaySeconds`**（G4） | v8 | 社区 `CatchupMinWaitMinutes` 已功能等价，仅刻度不同 | 极小 | 极低 |
| **P3** | **`ClearRecentTmdbCacheOnStartup`**（G2） | v8 | 社区有手动机制 + 缓存自动过期 | 小 | 低 |
| **P3** | **`ImageCapturePostProcessMode`**（G1） | v8 | 截图后处理（16:9/2:3/生成背景），只影响"无主图自动截图"的附加产物 | 中 | 低 |
| **P4** | **`LibraryScopePlayProgress`**（G3） | v8 | 跨库多版本播放进度；**动 `PresentationUniqueKey`，高风险** | 高 | **高** |

**另外 2 项「只验证、不移植」**：

| 项 | 动作 |
|---|---|
| **S1 搜索特殊符号** | 不照搬 PRO（源码不可还原）；改为**语料驱动**补符号（§六 阶段 0-C） |
| **S4 MovieDb 1.9.9.0** | 社区反射+缺失诊断天然容错；只需**实机验证一次**并把 `MovieDbVersionTier` 落进启动日志 |

---

## 五、明确不移植（清理清单）

| 项 | 理由 |
|---|---|
| `Portable.Licensing` 授权全套（`LicenseText`/`LicenseWarningStatus`/`ShowLicenseOptions`/`Part1`/`Part2`/`MITLicenseText`） | PRO 商业逻辑 |
| `BdInfoExaminer` / `BlurayMounter` 私有港 | 私有/GPL；社区自研 UDF 选片已覆盖 |
| `MonoMod` 补丁框架 | 与 Harmony 生态不互通 |
| `MemoryOptions` / `MemoryUsageLimitMB` / `SystemInfoItemList` | 社区 `EnableMemoryAutoRestart` 等**更优** |
| `SuppressPluginUpdates` / `UpdatePluginTask` | 社区主动移除（刻意决策，`170cd45`） |
| `LastProxyCheckSucceeded` | PRO 内部态，社区 `ProxyServerStatus` 已覆盖 |
| `ShowMetadataExtraOptions` | PRO 的 UI 折叠开关，无功能意义 |
| PRO 的"多选偏好+规则"UI 外壳（`MetadataRefreshPreferences+RuleList`、`SearchTuningOptionList`、`PersonRefreshOptionList` 等） | 语义等价，改造成本高、回归面大、收益近零 |
| `SyncMediaInfo`（分布式写回） | 需自建写回协议，风险高，当前无明确诉求 |
| `EnableAutomaticDecompression` | PRO 隐藏项；社区 `AltMovieDbConfig.cs:145` 已硬编码 `GZip\|Deflate` |

---

## 六、优化方案（分阶段可执行）

### 阶段 0 —— 本轮立即可做（预计半天，零风险）

**0-A 豆瓣客串演员中文化**（P0-A）

- 落点：`StrmAssistant/Provider/Douban/DoubanAssist.cs:635`
- 改法（`PersonType` 已实证为普通枚举，非 flags）：
  ```csharp
  // before
  p.Type == PersonType.Actor && !string.IsNullOrWhiteSpace(p.Name)
  // after
  (p.Type == PersonType.Actor || p.Type == PersonType.GuestStar) && !string.IsNullOrWhiteSpace(p.Name)
  ```
- 配套：`CelebrityRoleRegex`(:236) 的角色词从"仅 `饰[:：]`"扩到 `饰|客串|嘉宾`
- 验收：对一部含客串演员的剧集跑 `BuildDoubanCacheTask`，日志出现 `DoubanAssist - Cast X -> Y`（X 为客串演员原名）

**0-B 多版本 `MediaInfoExtended`**（P0-B）

- 落点（**四处必须同步**，v8 §四-3 红线）：
  1. `StrmAssistant/Options/ExperienceEnhanceOptions.cs:98-102` — 选项列表补 `MediaInfoExtended`
  2. `StrmAssistant/Properties/Resources.resx` + `.zh.resx` + `.zh-hant.resx` — 新增 `MultiVersionDisplayOption_MediaInfoExtended_*` 键
  3. `StrmAssistant/Properties/Resources.Designer.cs` — 同步生成属性
  4. `StrmAssistant/Web/Service/MultiVersionService.cs:117` — 新增分支
- **前置**：先抽一个 `BuildMediaInfoLabel(MediaStreams, Bitrate, Size)` 工具方法（`Common/` 下），
  产出形如 `4K HEVC 20.4Mbps` / `4K HEVC 20.4Mbps 45.2GB`
- 验收：多版本条目在 Emby 详情页显示含体积的标签；三语无缺；`dotnet build` 0 错误

**0-C 搜索特殊符号：语料驱动补漏**（S1，不照搬 PRO）

- 落点：`StrmAssistant/Mod/EnhanceChineseSearch.cs:89`（索引侧）+ `:103`（查询侧）
- 做法：从真实库名抽 top-N 含符号的条目，比对"加符号前后命中率"，**只补有实测收益的符号**
- 验收：目标条目搜索命中率提升，且**不得**引入误命中（用现有回归条目集跑一遍）

### 阶段 1 —— HTTP/strm ISO（P1，唯一大工作量）

> v8 §3.3 的 `bluray:"{0}"` 路线 **已实测失败**（见 §七），必须换路。

| 步 | 内容 | 落点 |
|---|---|---|
| **ISO-1'** | 放开 `://` + `.iso` 识别（跳过本地 `File.Exists` 校验） | `Common/IsoMounter.cs:89-93` |
| **ISO-2'** | HTTP ISO 走**分片下载 + UDF extent 解析**（替代全量挂载）：先取 ISO 头/尾 + UDF 元数据区，按需 Range 取 `PLAYLIST/*.mpls`、`STREAM/*.m2ts` | 新增 `Common/RemoteIsoReader.cs` |
| **ISO-3'** | 探测/截图输入指向本地缓存片段，**不改 `MediaSourceInfo.Path`** | `Mod/IsoExtraction.cs`、`Mod/EnableImageCapture.cs` |
| **ISO-4'** | 选项文案 + 三语 resx | `Options/MediaInfoExtractOptions.cs` + `Resources*` |

**红线**：① 不改 `MediaSourceInfo.Path`；② 能力不足自动回退不改写；③ 全程零新私有依赖。

### 阶段 2 —— 仪表盘 + 筛选菜单（P2，低风险）

- **搜索索引「优化」**：`Mod/EnhanceChineseSearch.cs` 新增 `OptimizeSearchIndex()`（对标 PRO 返回 `(Reclaimed, Bytes)?`），`Options/View/HomePageView.cs:169` 旁增一个 `case "OptimizeSearchIndex"`
- **筛选菜单**：新增 `Web/Api/GetFilterMenuHtml.cs`（`[Route("/{Web}/modules/filtermenu/filtermenu.template.html","GET")]`）+ 嵌入改过的模板资源 + 在 `ShortcutMenuHelper` 里注入 `HasDoubanId`/`HasImdbId`

### 阶段 3 —— v8 遗留 G1~G4（P3，锦上添花）

`CatchupMinimumDelaySeconds`（极小）→ `ClearRecentTmdbCacheOnStartup`（小）→ `ImageCapturePostProcessMode`（中）→ **`LibraryScopePlayProgress` 单独立项**（高风险，需专项验证 `PresentationUniqueKey` 链路）

### 通用验收基线（沿用 v8）

1. `dotnet build` 0 错误；ILRepack 产物部署两个 plugins 目录，MD5 一致
2. Emby 重启：插件加载、补丁数正常、Healthy、0 error
3. 新配置可见可改、重启持久化、**三语无缺**
4. patch 类改动在 4.9.1.90 与 4.10.x 双版本各跑一次

---

## 七、与 v8 计划的差异（哪些已落地 / 被推翻）

| v8 计划项 | 现状 | 证据 |
|---|---|---|
| **第 1 阶段 ISO-1~4** | ✅ **已落地**（`a48a2cd`/`1741f6d`/`06c9399`/`9e9bb8e`） | ISO 提取真正可用（Windows 原生挂载） |
| ↳ **ISO-2 的 `bluray:"{0}"` 协议** | ❌ **被推翻** | `IsoMounter.cs:26-27` 注释：实测 `bluray:` 协议在**文件路径/挂载盘/HTTP 上均失败**；Windows 版 libbluray 读不了 UDF |
| ↳ ISO-3 `FfmpegPathOverride` 能力位 | ✅ 保留（`SupportsBluray` 仍缓存，作为回退判据） | `Common/FfmpegPathOverride.cs:24-34,126-161` |
| **第 2 阶段 MPLS 语言/章节** | ✅ **已完成** | `1741f6d` + `c8e554c`（MPLS 章节 → `Chapters3` 实机验证通过） |
| **第 3 阶段 双层杜比 P7** | ✅ **已完成** | `c5dc966` |
| **第 4 阶段 ISO 截图/章节图** | ✅ **已完成** | `6adc50f` + `c8e554c`（`-skip_frame nokey` 修复） |
| **第 5 阶段 G2/G4** | ⬜ 未开始（本报告 §六 阶段 3） | — |
| **真缺口数量** | **4 项 → 7 项**（新增 `MediaInfoExtended`、豆瓣客串、搜索索引优化） | 本报告 §二/§四 |
| **"PRO 有 / 社区无" = 67 项** | 首次量化（v8 未给出总数） | §3.1 机器比对 |

> **一句话**：v8 的"唯一硬骨头是 ISO"判断**依然成立**，但 v8 给出的 `bluray:` 实现路线**已被实测否定**，需按 §六 阶段 1 换路；同时 v8 漏掉了 .55 的 3 个增量点（已在本报告补全）。

---

## 八、证据与可信度

| 结论 | 证据 | 等级 |
|---|---|---|
| PRO **无** changelog 文件 | `_pro_tmp/strings.txt` 全量检索 0 命中；仅含 GitHub Releases API URL | 实证（高） |
| PRO 官方更新日志全文 | GitHub Releases `sjtuross/StrmAssistant.Releases` 抓取 | 实证（高） |
| **PRO 源码行级 diff 无效** | `IgnoreOnPersistAttribute.cs` 32/64 行"差异"逐行核对 = 100% 重命名；`UpdatePluginTask.cs` 报 7266 行 > 文件 4363 行 | 实证（高） |
| **`MediaInfoExtended` 为 .55 新增** | ① `.53`/`.55` 枚举成员差集 ② 资源键 `.55` 独有 ③ 反平坦化渲染分支 `IL_0229 = Format(Streams, Bitrate, Size)` | 三重实证（高） |
| **社区豆瓣只处理 `Actor`** | `DoubanAssist.cs:635` 源码 | 源码实证（高） |
| **`PersonType` 非 flags**（须用 `\|\|`） | `ilspycmd -t MediaBrowser.Model.Entities.PersonType` on `mediabrowser.common/4.9.1.90` | 反编译实证（高） |
| **社区无 HasDoubanId 筛选** | `grep -r HasDoubanId` 全项目（含 `.js`/`.resx`）0 命中 | 机器比对（高） |
| **`bluray:` 路线失败** | `IsoMounter.cs:26-27` 实测注释 | 实测（高） |
| Options 212 vs 238 / 67 vs 93 | `_pro_tmp` 与项目 `Options/**` 机器提取（只取非混淆 `public` 属性） | 机器比对（高） |
| **S1 搜索符号集** | PRO 侧在加密段内，**无法还原** | 低（已标注） |
| **S6「其它已知问题修复」** | 不可枚举 | 不可知（已标注） |
| 67 项中"语义等价"44 项 | **人工判定** | 中（已标注） |

---

## 附：本次使用的比对脚本（可复现）

```bash
# 1) 语义锚点：Options 属性 + 枚举成员（跳过混淆名 A*/AA*/AAA*）
python /tmp/props2.py \
  "_pro_tmp/decompiled_v2/StrmAssistant/Options" \
  "StrmAssistant-myp015/StrmAssistant/Options"

# 2) 枚举成员级差异（.53 vs .55）
python /tmp/difftree.py          # 输出「only in 3.0.0.53 / 3.0.0.55」与逐文件变更行数

# 3) PRO 更新日志
curl -sL https://raw.githubusercontent.com/sjtuross/StrmAssistant.Releases/main/latest.json
# → {"tag_name":"v3.0.0.55"}   （正文须看 releases 页）
```
