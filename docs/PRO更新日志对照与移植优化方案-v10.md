# PRO 更新日志对照 × 项目现状 × 移植优化方案（v10）

> **PRO 基准**：`3.0.0.56+763a335`（`BuildTimeUtc 2026-10-16T16:33:46Z`，`netcoreapp6.0` / MonoMod）
> **项目基准**：`D:\下载\AI\StrmAssistant-myp015` @ **`6b29c60`**（`AssemblyVersion 3.0.0.56`，`net6.0` / `Lib.Harmony 2.4.2`）
> **PRO 参考源**：统一使用 **`D:\下载\AI\StrmAssistant.Releases`**（见 §1.1）
> **日期**：2026-10-20　**状态**：取代 `PRO更新日志对照与移植优化方案-v9.md`
> **本版核心结论**：PRO `.56` 相对 `.55` **功能面零变化**（语义锚点全等）；v9 列的 **7 项真缺口已闭环 4 项**，**只剩 3 项且全 P3**

---

## 〇、一页看懂：v9 → v10 变化

| 项 | v9（2026-10-17 写） | **v10（本版）** |
|---|---|---|
| PRO 基准版本 | 3.0.0.55 | **3.0.0.56**（实证与 .55 无功能差异） |
| PRO 参考目录 | `_pro_tmp/`（混放 .53 与 .55） | **`D:\下载\AI\StrmAssistant.Releases`**，`.56` 单独放 `_pro_56/` |
| 项目基线 | `c8e554c`（10-16） | **`6b29c60`**（10-17，含 18 个新提交） |
| Options 属性数（社区） | 238 | **274**（分组重构 +32） |
| PRO 有 / 社区无 | 67 | **63** |
| 社区有 / PRO 无 | 93 | **125** |
| **真缺口** | 7 项 | **3 项，全 P3**（G2 / G3 / G4） |
| 已闭环 | — | **S2 搜索索引优化、S3 MediaInfoExtended、S5 豆瓣客串、G1 截图后处理** |
| 唯一大工作量 | HTTP/strm ISO | **不变**（HTTP/strm ISO） |

---

## 一、PRO 参考源与版本基线

### 1.1 参考目录统一

以后 PRO 侧一律以 **`D:\下载\AI\StrmAssistant.Releases`** 为准：

| 路径 | 内容 | 版本 | 状态 |
|---|---|---|---|
| **`_pro_56/StrmAssistantPro_3.0.0.56.dll`** | **唯一保留的 PRO 程序集** | **3.0.0.56+763a335** | ✅ |
| **`_pro_56/decompiled/`** | .56 反编译树（140 个 `.cs`） | 同上 | ✅ |
| `_pro_tmp/decompiled_v2/` | .55 反编译树（138 个 `.cs`） | 3.0.0.55+3abcae8 | ✅ 保留 |
| `_pro_tmp/decompiled/` | .53 反编译树（141 个 `.cs`） | 3.0.0.53+2222f8a | ✅ 保留 |
| `_pro_tmp/strings.txt` / `classes.txt` / `decompile_report.md` | .53/.55 字符串与类清单 | — | ✅ 保留 |
| `plugins/` | MovieDb / Tvdb 各历史版本 zip（`MovieDb_10.9.9.0.zip` 已含） | — | ✅ |
| `static-ffmpeg/` `static-ffprobe/` `rffmpeg/` `sqlite/` `jieba-dict/` `stub/` | PRO 随包静态资源 | — | ✅ |
| ~~根目录 `StrmAssistantPro.dll`~~ | ~~.55 程序集（md5 `a8bb89c8…`）~~ | 3.0.0.55 | ❌ **已删除 2026-10-20** |
| ~~`_pro_tmp/StrmAssistantPro.dll`~~ | ~~.53 程序集（md5 `70c46d66…`）~~ | 3.0.0.53 | ❌ **已删除 2026-10-20** |

> **版本对应关系（2026-10-20 清理后）**
> - 旧的 `.53` / `.55` 两个程序集已按用户要求删除，**唯一保留的 PRO 二进制是 `_pro_56/StrmAssistantPro_3.0.0.56.dll`**。需要旧版时按 tag 从 GitHub Releases 重新下载即可（`.53` / `.55` 的 release 仍在）。
> - 三个反编译树全部完好：`_pro_tmp/decompiled` ← **.53**、`_pro_tmp/decompiled_v2` ← **.55**、`_pro_56/decompiled` ← **.56**。**删掉 dll 不影响任何比对工作**。
> - ⚠️ 命名风格不同：`.56` 树是**点扁平化**（`StrmAssistant.Options/…`），`.55` 树是**嵌套目录**（`StrmAssistant/Options/…`）。写脚本时不能写死路径，要用前缀过滤或 walk 整个根目录。

### 1.2 v3.0.0.56 实证：与 .55 **无功能差异**

| 检查项 | .55 | .56 | 差异 |
|---|---|---|---|
| `BuildTimeUtc` | 2026-10-15T03:46:09Z | **2026-10-16T16:33:46Z** | +1.5 天 |
| `InformationalVersion` | `3.0.0.55+3abcae8` | `3.0.0.56+763a335` | 提交不同 |
| 文件体积 | 5,292,032 B | **5,305,856 B** | **+13,824 B** |
| **全量 public 属性名** | 860 | **860** | **差集空** |
| **资源键**（`DescriptionL`/`DisplayNameL`） | 262 | **262** | **差集空** |
| **枚举类型数 / 成员** | 26 | **26** | **成员差集空**（仅混淆名轮换） |
| Release 更新日志正文 | — | — | **逐字相同** |
| `.cs` 文件数 | 136 | 138 | 仅 2 个混淆目录改名 |

**结论**：`.56` 是 **`.55` 的热修复重打包**——二进制变了（体积 +13.8 KB），但**选项、枚举、资源键、功能面全部零变化**。

→ **你已移植的 .55 那批修复，即已覆盖 .56。不需要再为 .56 做任何移植动作。**

### 1.3 PRO 更新日志从哪来

PRO **没有 changelog 文件**（`strings.txt` 全量搜 `changelog|releasenote|更新日志` = 0 命中），唯一权威来源是 **GitHub Releases 正文**（`sjtuross/StrmAssistant.Releases`）——PRO 自己的 `UpdatePluginTask` 也是从那里取。

已抓全 **v3.0.0.45 → v3.0.0.56**（`.54` 未发 release）。

### 1.4 ⚠️ 方法论红线：PRO 源码**不能**做行级 diff

PRO 逐构建重新混淆（Unicode 类名 + 控制流平坦化 + 字符串加密）。实测：
- `IgnoreOnPersistAttribute.cs` 报 32/64 行差异 → 逐行核对 **100% 是重命名**
- `UpdatePluginTask.cs` 报 **7266 行**差异 > 文件本身 4363 行

**本报告一律走语义锚点比对**（属性名 / 枚举成员 / 资源键 / 路由 / AssemblyInfo），本版 §1.2 即是该方法的又一次成功应用。

---

## 二、.53 → .56 全部更新点的落地状态

| # | 更新点（.55/.56 日志） | 社区版状态 | 落地证据 |
|---|---|---|---|
| **S1** | 中文搜索增强 — 特殊符号处理 | 🟡 **部分**（PRO 侧在加密段，无法逐字还原） | `EnhanceChineseSearch` 索引侧剥 `'` `.` `·` `-`，查询侧剥 `·` `-`；`fd8656c feat(search): 中文搜索简繁互通与间隔号/连字符索引清理` |
| **S2** | 仪表盘支持搜索索引**重建** | ✅ **已移植，且超出** | `EnhanceChineseSearch.ScheduleIndexRebuild()`(:62) + `RuntimeStatusOptions.cs:29` 按钮；**另**实现 `OptimizeSearchIndex()`(:84) 返回 `(ReclaimedPages, ReclaimedBytes)?` + 按钮(:36) → 同时覆盖了 `.53` 的"优化"项 |
| **S3** | 多版本美化**新增显示格式** | ✅ **已移植** | `MultipleVersionsOptions.cs:25 MediaInfoExtended` + `VersionDisplayOptions.cs` + 三语 resx；下拉显示 **默认 / 画质 / 画质+体积 / 文件名** |
| **S4** | 适配 MovieDb **1.9.9.0** | 🟡 **等价**（反射+缺失诊断天然容错） | `ChineseMovieDb.cs:90-110` 缺失组件告警；`MetadataEnhanceOptions.cs:390-415 MovieDbVersionTier`（`1.9.9.0+` 分档）；**仅缺 1.9.9.0 实机验证** |
| **S5** | 豆瓣中文化**未处理客串演员** | ✅ **已移植** | `DoubanAssist.cs:1110` → `(p.Type == PersonType.Actor \|\| p.Type == PersonType.GuestStar)`；`eaf5955 feat(stage0)` |
| **S6** | 其它已知问题修复和性能优化 | ⚪ **不可枚举** | — |

> **结论：6 条里 3 条完全闭环、2 条等价/部分、1 条不可枚举。没有遗留的"必做项"。**

---

## 三、全量能力面逐层对照（更新到 `6b29c60`）

### 3.1 Options 层 —— 机器比对

```
PRO  .56 Options 属性 = 212      社区 @6b29c60 Options 属性 = 274
PRO 有 / 社区无      = 63        社区有 / PRO 无      = 125
```

**63 项"PRO 有 / 社区无"三分类：**

| 分类 | 数量 | 说明 |
|---|---|---|
| **A. 真功能缺口** | **3** | `CatchupMinimumDelaySeconds`(G4) / `ClearRecentTmdbCacheOnStartup`(G2) / `LibraryScopePlayProgress`(G3) |
| **B. 命名/形态差异（语义等价）** | **44** | 见 §3.2 |
| **C. 明确不移植** | **16** | 授权 8 项、PRO 内部态 3 项、已被社区更优实现取代 3 项、框架差异 5 项（有重叠） |

### 3.2 形态等价对照表（44 项，逐条映射）

| PRO 属性 | 社区对应实现 | 位置 |
|---|---|---|
| `BeautifyMultiVersion` | `EnableMultiVersionEnhance` | `VersionDisplayOptions.cs` |
| `MultiVersionDisplayPreference` | `MultiVersionDisplayOption`（已枚举化） | `VersionDisplayOptions.cs:27` |
| `MultiVersionReorderPreference` | `MultiVersionSortOption`（已枚举化） | `VersionDisplayOptions.cs:34` |
| `EnableDoubanAssistScraping` | `EnableDoubanAssist` | `DoubanAssistOptions.cs` |
| `EnforceUserPreferences` | `EnforceUserPreferencesScope` + `EnforceLibraryOrder` | `UIFunctionOptions.cs` |
| `EnhanceChineseSearchMode` | `EnhanceChineseSearch` + `EnhanceChineseSearchRestore` | `ChineseSearchOptions.cs` |
| `SearchTuningOption(List)` / `SearchTuningPreferences` | `MixedChineseVariants` / `ExcludeOriginalTitleFromSearch` / `DigitsAsTmdbId` / `ShowMediaInfo` / `BlockSearchRecommend` | `ChineseSearchOptions.cs` + `UIFunctionOptions.cs` |
| `SearchIndexOption(List)`（Full/Lite） | 按 `MixedChineseVariants` 自动选 `unicode61` / `simple` 分词器 | `EnhanceChineseSearch.cs` |
| `FingerprintExcludeKeywords` | `FingerprintBlacklistKeywords` | `IntroSkipFingerprintOptions.cs` |
| `IntroDbProvider` | `UnifiedIntroDbProviders` | `IntroSkipScrapeOptions.cs` |
| `IntroSkipFingerprintOptions` / `IntroSkipPlaybackOptions` / `IntroSkipScrapeOptions`（嵌套） | 同名三类**已拆分** | `IntroSkipFingerprintOptions.cs` 等 |
| `MarkPreviousEpisodesPlayed(+Threshold)` | `EnableAutoMarkWatched` + `AutoMarkWatchedThreshold` | `ExperiencePlaybackOptions.cs` |
| `MetadataRefreshOptions` / `MetadataBuildOptions` / `MetadataScrapeOptions`（嵌套） | `MetadataRefreshOptions` / `MetadataCacheOptions` / `ScrapeOptimizeOptions` | `MetadataEnhanceOptions.cs` 分组 |
| `MetadataRefreshPreferences+RuleList` / `PersonRefreshOption(List)` | `EpisodeRefreshOptionList` / `RefreshPersonOptionList`（多选化） | `MetadataRefreshOptions.cs` / `PersonEnhanceOptions.cs` |
| `ProxyModeOption` | `ProxyRoutingMode` | `NetworkOptions.cs` |
| `Is*PluginSupported`（4 个） | `Is*PluginLoaded` | 各分组类 |
| `EnableAutomaticDecompression`（隐藏项） | `AltMovieDbConfig.cs:145` 硬编码 `GZip\|Deflate` | `Mod/AltMovieDbConfig.cs` |
| `DoubanClient` | `DoubanCookie` / `DoubanAccessToken` | `DoubanAssistOptions.cs` |
| `DoubanScrapeButton` / `DoubanScrapeProgress` | 社区有 `ClearSeasonMapButton` 等按钮，无豆瓣刮削进度按钮 | `DoubanAssistOptions.cs` |
| `ShowMetadataExtraOptions` | 不移植（PRO 的 UI 折叠开关） | — |

### 3.3 社区有 / PRO 无（125 项）—— 反超面，禁止误删

| 模块 | 代表属性/实现 |
|---|---|
| **影音直链** | `EnableStrmDirectRedirect` / `StrmDirectRedirectFollow302` / `StrmVideo·AudioDirectRedirectClientBlacklist` / `EnableLiveTvDirectRedirect`（PRO 完全没有） |
| **内存运维** | `EnableMemoryAutoRestart` / `MemoryAutoRestartThresholdMB` / `EnableMemoryCleanup` / `CleanMemoryNowButton` / `RuntimeStatusList` / `OptimizeSearchIndexButton` |
| **豆瓣细分** | `DoubanCastChinese` / `DoubanPosterFallback` / `DoubanRatingOverride` / `DefaultNewLibraryTmdb` / `ClearSeasonMapButton` |
| **ISO 开关** | `EnableIsoExtraction`（PRO 内建无开关） |
| **中文搜索** | `BlockSearchRecommend` / `ExcludeOriginalTitleFromSearch` / `MixedChineseVariants` / `DigitsAsTmdbId` / `IsChineseSearchSupported` |
| **抓取代理** | `ScrapeProxyWhitelist` / `ShowScrapeProxyWhitelist` / `ProxyRoutingMode` |
| **元数据缓存** | `IsMetadataCacheEnabled` / `IsMetadataCacheReadEnabled` / `IsMetadataCacheWriteEnabled` / `MetadataCacheModeList` |
| **MovieDb 版本诊断** | `MovieDbPluginVersion` / `MovieDbVersion` / `MovieDbVersionTier` / `IsMovieDbVersionCompatible` |
| **品牌区 / 总开关** | `BrandOptions` / `BrandItemList` / `EnablePlugin` |
| **UI 框架层** | `Caption` / `SubCaption` / `ShowBack` / `ShowSave` / `OKButtonCaption` / `WizardHidingBehavior` 等 |

### 3.4 ScheduledTask / Web / Provider / Mod

| 层 | 结论 |
|---|---|
| **ScheduledTask** | ✅ **无真缺口**（PRO 14 个任务，社区 11 个；差异全是命名/形态；`UpdatePluginTask` 社区主动删除） |
| **Web/Api** | 社区聚合 `GetStrmAssistantJs` + `ShortcutMenuHelper`；**仍缺** `GetFilterMenuHtml`（HasDoubanId/HasImdbId 筛选，`grep` 全项目 0 命中）与 `SyncMediaInfo` |
| **Provider** | ✅ 等价（PRO `.55` 已删 `MovieDbSeriesProvider`，社区仍保留 → 社区多） |
| **Mod 层** | **社区独有 39 个补丁文件**，PRO 为 0（全在 MonoMod 里） |
| **Core 层** | **社区独有 11 个适配类**，PRO 为 0 |

---

## 四、剩余真缺口（仅 3 项，全 P3）

| # | PRO 属性 | 语义 | 社区现状 | 工作量 | 风险 | 建议 |
|---|---|---|---|---|---|---|
| **G2** | `ClearRecentTmdbCacheOnStartup` | 启动时清 TMDB 最近缓存 | 有 `LocalTmdbCache` 手动机制，缓存本就会过期失效 | 小 | 低 | **可做可不做** |
| **G3** | `LibraryScopePlayProgress` | 按媒体库分别记录多版本播放进度（仅剧集） | 无 | 高 | **高**（动 `PresentationUniqueKey`） | **不主动做**，除非确有跨库同剧需求 |
| **G4** | `CatchupMinimumDelaySeconds` | 追更最小延迟（**秒**粒度） | `CatchupMinWaitMinutes`（**分钟**粒度，功能等价） | 极小 | 极低 | **想精细就做**，否则保持分钟级即可 |

> v9 里的另外 4 项（`MediaInfoExtended`、豆瓣客串、截图后处理、搜索索引优化）**已全部闭环**，见 §2 与 §五。

---

## 五、项目侧 10-17 新增能力（v9 之后落地，供追溯）

| 能力 | 提交 | 说明 |
|---|---|---|
| **设置页 6 Tab 重排** | `bbea45d` | 基础设置 / 体验增强 / 媒体信息提取 / 元数据增强 / 片头片尾 / 网络与直链 |
| **分组小标题** | `bbea45d` `5e5ad2c` | 每个 Tab 下用嵌套 `EditableOptionsBase` 出小标题：元数据 **8 组**（中文元数据/剧集组/刮削优化/人物增强/豆瓣辅助/海报与排序/元数据刷新/本地元数据缓存）、提取 **5 组**、片头 **3 组**、体验 **3 组** |
| **旧配置自动迁移** | 各 `*OptionsMigration.cs` | 平铺旧键 → 分组新键，升级不丢配置 |
| **版本号显示修复** | `c06f6e4` `5e5ad2c` | `AboutOptions.GetVersion()` 只返回程序集版本，不再拼 `+hash`；版本行移到品牌区 |
| **多版本下拉中文化** | `bbea45d` | `string` → **枚举**（自动下拉）+ `DescriptionL` 三语 |
| **截图后处理（G1 闭环）** | `919011e` | `ImageCaptureOptions.ImageCapturePostProcess`（16:9 / 2:3 / 生成背景图）+ `Common/ImagePostProcessor.cs`；修 `%MetadataPath%` 解析 |
| **搜索索引优化（S2 闭环）** | `325a916` `fd8656c` | 重建 + 空间回收双按钮 |
| **HDR Vivid** | `b3ab5cc` | 探测→落库→显示→标签全链路 |
| **豆瓣季映射清理 / 评分快照** | `997707f` `b3d36c7` | 季映射清理按钮 + 匹配日志置信度；评分快照缺失导致徽章 7 天不显示 |
| **前端 observer 合并 / 选项搜索 / 重启失败提示** | `4d19df6` | 性能与易用性 |
| **tokenizer 每连接初始化** | `c0a42f4` | 修复中文分词在部分库连接上不生效 |
| **resx 键一致性校验** | `082b5f5` | `tools/check_resx_keys.py` 构建前校验（防 §v9 记的 `Designer.cs` 漏同步） |

---

## 六、优化方案（更新后）

### 阶段 0 —— 已闭环 ✅

v9 列的阶段 0 三项（豆瓣客串、`MediaInfoExtended`、搜索符号）中前两项已完成；第三项改为"语料驱动补符号"，仍未做但**不阻塞**。

### 阶段 1 —— HTTP/strm ISO 全链路（**唯一大工作量**）

> v8 的 `bluray:"{0}"` 路线**已实测否定**（`IsoMounter.cs:26-27`：bluray 协议在文件路径/挂载盘/HTTP 上均失败），必须换路。

| 步 | 内容 | 落点 |
|---|---|---|
| **ISO-1'** | 放开 `://` + `.iso` 识别（跳过本地 `File.Exists` 校验） | `Common/IsoMounter.cs` |
| **ISO-2'** | HTTP ISO 走**分片 Range 下载 + UDF extent 解析**（先取 ISO 头/尾 + UDF 元数据区，按需取 `PLAYLIST/*.mpls`、`STREAM/*.m2ts`） | 新增 `Common/RemoteIsoReader.cs` |
| **ISO-3'** | 探测/截图输入指向本地缓存片段 | `Mod/IsoExtraction.cs`、`Mod/EnableImageCapture.cs` |
| **ISO-4'** | 选项文案 + 三语 resx | `Options/IsoToolOptions.cs` + `Resources*` |

**红线**：① 不改 `MediaSourceInfo.Path`；② 能力不足自动回退；③ 零新私有依赖。

### 阶段 2 —— 筛选菜单（低风险，可选）

`Web/Api/GetFilterMenuHtml.cs`（`[Route("/{Web}/modules/filtermenu/filtermenu.template.html","GET")]`）+ 嵌入改过的模板 + 在 `ShortcutMenuHelper` 注入 `HasDoubanId` / `HasImdbId`。

### 阶段 3 —— 剩余 3 项 P3

`CatchupMinimumDelaySeconds`（极小，看需求）→ `ClearRecentTmdbCacheOnStartup`（小）→ **`LibraryScopePlayProgress` 单独立项**（高风险，需专项验证 `PresentationUniqueKey`）。

### 通用验收基线

1. `dotnet build` 0 错误；`tools/check_resx_keys.py` 通过；ILRepack 产物部署两处 plugins，MD5 一致
2. Emby 重启：插件加载、补丁数正常、Healthy、0 error
3. 新配置可见可改、重启持久化、**三语无缺**
4. patch 类改动在 4.9.1.90 与 4.10.x 双版本各跑一次

---

## 七、与 v8 / v9 的差异（勘误汇总）

| 文档 | 结论 | v10 勘误 |
|---|---|---|
| v8 | ISO-2 用 `bluray:"{0}"` | ❌ **实测失败**，改走 Windows 原生挂载（本地）/ 分片 Range（远程） |
| v8 | 真缺口 4 项（G1~G4） | G1 已闭环 → **剩 G2/G3/G4** |
| v8 | 功能对齐 ~92% | 按属性集算：PRO 212 项中，社区**语义已覆盖 209 项**（真缺口 3 项）≈ **98.6%** |
| v9 | 真缺口 7 项 | **4 项已闭环**，剩 3 项 |
| v9 | PRO 基准 .55 | 基准更新为 **.56**（与 .55 功能等价） |
| v9 | 参考目录 `_pro_tmp` | 统一到 **`D:\下载\AI\StrmAssistant.Releases`** |

---

## 八、证据与可信度

| 结论 | 证据 | 等级 |
|---|---|---|
| **.56 与 .55 功能面零变化** | ① 全量 public 属性 860=860 差集空 ② 资源键 262=262 差集空 ③ 枚举 26=26 成员差集空 ④ changelog 逐字相同 ⑤ AssemblyInfo 构建时间不同、体积 +13,824 B | 五重实证（高） |
| PRO 无 changelog 文件 | `strings.txt` 全量检索 0 命中；仅含 GitHub Releases API URL | 实证（高） |
| PRO 源码行级 diff 无效 | `IgnoreOnPersistAttribute.cs` 32/64 行"差异"逐行核对 = 100% 重命名 | 实证（高） |
| Options 212 vs 274 / 63 vs 125 | `.56` 反编译树与项目 `Options/**` 机器提取（只取非混淆 `public` 属性） | 机器比对（高） |
| 真缺口仅 3 项 | 63 项逐项人工核对（44 项映射见 §3.2） | 人工核对（中高） |
| `PersonType` 非 flags | `ilspycmd -t MediaBrowser.Model.Entities.PersonType` on `mediabrowser.common/4.9.1.90` | 反编译实证（高） |
| `bluray:` 路线失败 | `IsoMounter.cs:26-27` 实测注释 | 实测（高） |
| **S1 搜索符号集** | PRO 侧在加密段内，**无法还原** | 低（已标注） |
| **S6「其它已知问题修复」** | 不可枚举 | 不可知（已标注） |

---

## 附：可复现脚本

```bash
# 0) 网络：环境代理对 GitHub 返回 502，必须直连
export GIT_DIRECT="env -u http_proxy -u https_proxy -u HTTP_PROXY -u HTTPS_PROXY"

# 1) 拉取 .56 程序集
curl -L --noproxy '*' -o _pro_56/StrmAssistantPro_3.0.0.56.dll \
  https://github.com/sjtuross/StrmAssistant.Releases/releases/download/v3.0.0.56/StrmAssistantPro.dll

# 2) 反编译
"C:/Users/liwl/.dotnet/tools/ilspycmd.exe" -p -o "_pro_56/decompiled" "_pro_56/StrmAssistantPro_3.0.0.56.dll"

# 3) 语义锚点比对（属性 / 资源键 / 枚举）——.55 vs .56
python /tmp/anchor.py "_pro_tmp/decompiled_v2" "_pro_56/decompiled"

# 4) Options 属性集比对——PRO vs 项目
python /tmp/props3.py "_pro_56/decompiled" \
  "StrmAssistant/Options" "StrmAssistant.Options" ""
```
