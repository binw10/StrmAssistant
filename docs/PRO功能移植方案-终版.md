> 本地工程：`D:\下载\AI\StrmAssistant-myp015`（分支 `myp015`，HEAD `a8b5cd5`；插件已更名 **StrmAssistant Plus / 神医助手 Plus**，版本 **3.0.54**，net6.0）
> 参照对象：PRO **v3.0.0.53**（`_pro_tmp/decompiled` 反编译，由 `Properties/AssemblyInfo.cs` 的 `AssemblyVersion=3.0.0.53` 证实）；GitHub Releases 最新为 **v3.0.0.55**（2026-10-15）
> 本版为 **v3**，替代并修正 v2（`--终版.md` 旧内容）与 v1（`PRO功能移植方案.md`）；日期 2026-10-15
> 方法论：**以源码为准**。v2 的缺口矩阵有大量行未随 P2/P3 提交更新，本版逐项重新核对代码。

---

## 〇、三项目定位（重定基线）

| 代号 | 实体 | 关键事实 |
|---|---|---|
| **社区版** | `sjtuross/StrmAssistant` 免费开源版 | Wiki 明确：仅支持 Emby **4.8.5.0–4.8.11.0**、MovieDb ≤1.8.6，**不再适配其它版本**；功能更新全部转入 PRO |
| **本地分支** | 本工作区（社区版底座 + AI 适配 4.9.1.x/4.10.x） | 已叠加豆瓣辅助、IntroDb 双源、影音直链、运维状态区、版本适配层 `Core/`，并已落地 P2/P3 |
| **PRO** | `sjtuross/StrmAssistant` 付费版 | 反编译基线 **v3.0.0.53**；`StrmAssistantPro.csproj` 目标框架 `netcoreapp6.0`，依赖含 **BlurayMounter**、MovieDb、NfoMetadata、Tvdb |

> ⚠️ 基线更正：v2 写的「PRO = v3.0.0.54」是错的，反编译实为 **.53**；`latest.json` 现指向 **.55**。`.54/.33/.38` 无官方 Release 说明。

### 决策记录（沿用 v2，未变）
1. `SuppressPluginUpdates` **维持删除**（26.3.25 CHANGELOG 为刻意移除）。本次不重开。
2. `LocalDiscoveryUrl` 挪至 P2/P3。
3. `EnforceLibraryOrder` 与 PRO `EnforceUserPreferences`（强制季集显示样式）**语义不同，不等价**——后者仍是缺口。
4. 在制品（`FolderVideoGroupingLimit`、`FfmpegPathOverride`、分库声纹、提取超时、黑名单）**均已提交**。

---

## 一、核对方法（本轮）

1. 枚举本地全部 `Options/*.cs` 公开配置属性 + `Mod/*.cs`、`ScheduledTask/*.cs`、`Web/**`、`Core/**` 文件清单（`git ls-files`，以提交为准）。
2. 对每条「PRO 更新日志宣称的功能」反查本地源码**是否存在实现点**（不只是配置项，含 patch/任务/服务）。
3. 对 v2 中被判为缺口但疑似的项，逐条打开代码确认（本轮推翻/改判若干）。
4. PRO 侧用 `decompiled/StrmAssistant.Properties/Resources.cs` 的资源键反查功能确实存在。

---

## 二、已移植面（本轮代码级确认，附证据）

### 2.1 配置面（已具备）

| 模块 | 已落地项 | 证据 |
|---|---|---|
| 网络 | 代理**双模式** `ProxyMode`(Global/ScrapeOnly) + `ScrapeProxyWhitelist` | [NetworkOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/NetworkOptions.cs#L28-L48) |
| UI/体验 | `ReplaceUnplayedWithTotal`、`ShowLibraryItemCount`、`ReverseLogLines`、`BlockSearchRecommend`、`HidePersonOption`(NoImage/ActorOnly/**NonChinese**)、`BeautifyMissingMetadata`、`EnhanceMissingEpisodes`、`NoBoxsetsAutoCreation` | [UIFunctionOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/UIFunctionOptions.cs#L25-L86) |
| 体验增强 | `FolderVideoGroupingLimit`（解 Emby 8 部上限）、`MetadataUpdateTrackedFields`、`EnhanceNotificationSystem`、`EnableDeepDelete`、`EnableMultiVersionEnhance`、`OptimizeSubtitle` | [ExperienceEnhanceOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/ExperienceEnhanceOptions.cs#L65-L114) |
| 媒体信息 | `ImageExtractionTimeoutMultiplier`、`MediaInfoExtractionTimeoutMs`、`ExclusiveExtract`+`ExclusiveControlFeatures`、`PersistMediaInfoMode`、`MediaInfoJsonRootFolder`、`CustomProbePath`/`CustomEncoderPath`、5 组黑名单 | [MediaInfoExtractOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MediaInfoExtractOptions.cs#L51-L87) 与 [#L156-L166](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MediaInfoExtractOptions.cs#L156-L166) |
| 片头 | `LibraryFingerprintMinutes`（分库声纹时长）、`ParallelMarkerComputation`、`IntroDbApiKey`、`TidbApiKey`、`UnifiedIntroDbProviders`、`MarkerEnabledLibraryScope`、双黑名单 | [IntroSkipOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/IntroSkipOptions.cs#L32-L93) |
| 元数据 | `EnableMovieDbEpisodeGroupExtendedInfo`、`DoubanAssistScrapeScope`、`MdbListApiKey`、`ClearTagline`、`PreferTraditionalChinese`、`DefaultNewLibraryTmdb`、`DoubanCastChinese`、`DoubanPosterFallback`、`OptimizeMovieDbEpisodeScraping`+`DisableMovieDbEpisodeScrapingFallback`、`DisableEpisodeImageScraping`、`AltMovieDb*` | [MetadataEnhanceOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MetadataEnhanceOptions.cs#L74-L165) |
| 通用/运维 | `CatchupMode`+`CatchupMinWaitMinutes`+`CatchupTaskScope`(4 任务)、`EnableMemoryAutoRestart`+阈值（含倒计时通知）、`MaxConcurrentCount`、`Tier2MaxConcurrentCount`、`EnableMemoryCleanup`、`RuntimeStatusList`、`ScheduleIndexRebuildButton` | [GeneralOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/GeneralOptions.cs#L21-L112) |
| 搜索 | `EnhanceChineseSearch`、`EnhanceChineseSearchRestore`、`SearchScope`、`ExcludeOriginalTitleFromSearch` | [ModOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/ModOptions.cs) |

### 2.2 实现面（Mod / Task / Service）

- **Mod（37 个）**：`OptimizeSubtitle`、`DefaultTmdbLibrary`（新建库默认 TMDB）、`MovieDbEpisodeGroup`、`PreferTraditionalChinese`、`ClearTagline`、`EnhanceNotificationSystem`、`EnforceLibraryOrder`、`MergeMultiVersion`（含 `FolderGroupingLimitTranspiler`）、`HidePersonNoImage`、`EnhanceChineseSearch`、`AltMovieDbConfig`、`ExclusiveExtract` 等。
- **ScheduledTask（12 个）**：新增/已确认 `BuildDoubanCacheTask`（豆瓣离线缓存）、`ScrapeIntroDbTask`、`ScanExternalSubtitleTask`（**已含外挂音轨扫描**）、`PersistMediaInfoTask`。
- **Provider/**：`Douban/DoubanAssist`、`MovieDbSeriesProvider`（`EnhanceMissingEpisodes` 的 TMDB 支持）、`MovieDbEpisodeGroupExternalId`、`MovieDbResponse/*`。
- **Core/**（本地独有，非 PRO 架构）：`EmbyVersionAdapter`/`EmbyApiVersion`（版本分档 4.9.1 / 4.9.1.80 / 4.9.1.90 / 4.9.2）、`FastReflection`、`HealthCheck`、`MemoryGuard`/`MemoryCleaner`、`PerformanceMonitor`/`PerformanceReporter`、`OptimizationAdvisor`、`ServiceLocator`。
- **IntroSkip/**：`PlaySessionMonitor`+`PlaySessionData`（播放会话内片头/片尾实时标记）。
- **Notification/**：`CustomNotifications`。

**结论**：v2 矩阵里标 ❌ 的约 20 项（`ImageExtractionTimeoutMultiplier`、`ClearTagline`、`DoubanAssistScrapeScope`、`MdbListApiKey`、`PreferTraditionalChinese`、新建库默认 TMDB、`OptimizeSubtitle`、豆瓣磁盘缓存、外挂音轨扫描、代理双模式、`TidbApiKey`/`UnifiedIntroDbProviders`、`ShowLibraryItemCount`/`ReplaceUnplayedWithTotal`/`ReverseLogLines`、`Pairallel...`）**实际已落地**。v2 的缺口矩阵不可再作为工作依据。

---

## 三、真实缺口矩阵（以代码为准，🔧部分 ❌缺）

### P1 · 收尾小项（低风险，可立即做）
| 项 | 状态 | 说明 |
|---|---|---|
| `ClearEmbeddedMetadata`（清内嵌元数据） | ❌ | 本地无此开关（`MediaInfoExtractOptions` 已全文核对） |
| 搜索词转换（繁→简，语音搜索） | ✅ | **已覆盖**：`NormalizeQueryTerm` 挂在 SQLite `@SearchTerm` 绑定参数 + 索引侧 `NormalizeSearchTerm` 双侧归一化 |
| `RatingUpdateLookbackDays`（豆瓣评分回看天数） | ❌ | PRO 在 `MetadataRefreshOptions`；本地为即时评分 |
| `EnableAutomaticIntroScraping` | ✅ | **已等价实现**：`Plugin.OnRefreshCompleted` 在 `EnableIntroDbScrape` 开启时自动对刷新完成的 Series/Movie 触发 `ScrapeSeriesAsync/ScrapeMovieAsync` |
| 多版本排序/美化细分偏好组 | 🔧 | 本地 `EnableMultiVersionEnhance` 已实现主体，缺 PRO 的 `MultiVersionDisplayOption`/`SortOption`/`ReorderPreference` 细分 |

### P2 · UI / 前端注入补完
| 项 | 状态 | 说明 |
|---|---|---|
| `OptimizeMediaItemOrder` + `OptimizeItemOrderPreferences`（四选） | ❌ | PRO `UIFunctionOptions.cs:162-166`；消费点 `A----------\AA---------.cs:5459` |
| `ShowCollectionsFromSeasons`（季的合集归属） | ❌ | PRO `UIFunctionOptions.cs:787-789`；消费点 `AA---------.cs:5254` |
| `EnforceUserPreferences` + `EnforceUserPreferencesScope`（强制季集显示样式） | ❌ | 与本地 `EnforceLibraryOrder` **不等价**；消费点 `AA---------\A----------.cs:13246` |
| `HidePersonOption.NonChinese` 已是本地三选 | ✅ | 已在 `UIFunctionOptions.cs:31` |
| JS 注入套件扩展（`GetIndicatorJs`/`GetItemJs`/`GetListJs` 等） | 🔧 | 本地为单入口 [GetStrmAssistantJs.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Api/GetStrmAssistantJs.cs) + [strmassistant.js](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Resources/strmassistant.js)；`BlockSearchRecommend`/`ShowLibraryItemCount`/`ReplaceUnplayedWithTotal` 都靠它 |

### P3 · 数据源与版本适配收尾
| 项 | 状态 | 说明 |
|---|---|---|
| **MovieDb 1.9.9.0 适配**（v3.0.0.55） | ❌ | 本地 `IsMovieDbPluginLoaded` 仅查程序集名，**无版本分档**：[MetadataEnhanceOptions.cs:247-248](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MetadataEnhanceOptions.cs#L247-L248) |
| **HDR Vivid 检测**（v3.0.0.53） | ❌ | [MultiVersionService.cs:151-159](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Service/MultiVersionService.cs#L151-L159) 只识别 DV/HDR10+/HDR，无 Vivid |
| **仪表盘搜索索引重建**（v3.0.0.55） | 🔧 | 本地有 `ScheduleIndexRebuildButton`（[GeneralOptions.cs:52-57](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/GeneralOptions.cs#L52-L57)），需核对是否等价重建逻辑 |
| 跨刮削器中文海报排序 / 混合库豆瓣图标（v3.0.0.53） | 🔧 | 部分在 `MultiVersionService`/`DoubanService` |
| `LocalDiscoveryUrl` 自定义本地发现地址 | ❌ | `NetworkOptions` 无此字段（已全文核对） |
| `MetadataRefreshPreferences` / `PersonRefreshPreferences` 多选 | 🔧 | 本地为 `EpisodeRefreshScope`/`RefreshPersonMode` 单值形态，功能近似、形态不同 |

### P4 · 重型能力（详见第四节）

---

## 四、P4 五项确认（用户猜测的「硬项」逐项答复）

**结论：5 项中 4 项确实未做；「分布式提取」只做到「配置形态」的一半。** 且相关代码当前**主动排除**该能力入口。

| # | 项 | 是否已做 | 代码证据 | 工作量 |
|---|---|---|---|---|
| 1 | **本地 TMDB 刮削源** | ❌ **未做** | 全库 grep `MetadataCacheMode`/`MetadataCacheRootFolder`/`MetadataCacheOptionalImageTypes`/`ClearRecentTmdbCacheOnStartup` **零命中**。PRO 侧：`MetadataEnhanceOptions.cs:1103-1170`（缓存目录校验）+ `Resources.cs:505` | **大**：需设计缓存目录结构/失效策略 + patch MovieDb provider 的 HTTP/序列化层 |
| 2 | **ISO/BDMV 提取** | ❌ **未做**（且被强制排除） | [LibraryApi.cs:61](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L61) `.Concat(new[] { MediaContainers.Iso })`、[:83](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L83) `.Concat(MediaContainers.Iso.GetAliases())` — 硬编码兜底排除；无 BlurayMounter、无 mpls 解析 | **大**：需自建 ProcessRunner + 静态 ffprobe 分段探测；`IMediaMountManager` 基建已有（[LibraryApi.cs:940](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L940) 反射挂载） |
| 3 | **音乐持久化** | ❌ **未做** | [PersistMediaInfoTask.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/ScheduledTask/PersistMediaInfoTask.cs) 全文件无 `Audio`/`Album`/`Artist`/内嵌封面分支（仅视频媒体信息） | **中**：Alumb/Artist 序列化 DTO + 写回白名单（音乐库刷新频繁，冲突面大） |
| 4 | **分布式提取** | 🔧 **仅配置形态** | 插件侧**无** `SyncMediaInfo` API（grep 零命中）；本地路线是 `CustomProbePath` 指向 rffmpeg wrapper（[MediaInfoExtractOptions.cs:157](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MediaInfoExtractOptions.cs#L157)）。PRO 侧有 `POST /Items/SyncMediaInfo`（`SyncMediaInfo.cs:12-24`）+ `MediaInfoService.cs:563-599` 写回 | **小-中**（插件侧）：若接 API 调度需补 Web 端点；若走 rffmpeg 则基本已通，剩运维 |
| 5 | **跨库多版本进度** | ❌ **未做** | `LibraryScopePlayProgress` grep 零命中。PRO 侧：`UIFunctionOptions_LibraryScopePlayProgress` 资源 + 消费点 `A----------\AA---------.cs:5304` | **大（高风险）**：需改 `PresentationUniqueKey` 计算链路，影响播放进度/收藏键 |

---

## 五、bug / 版本适配风险清单（本轮机读 + 人工复核）

### 5.1 已确认的脆弱点

1. **Transpiler 单形态匹配** — [MergeMultiVersion.cs:240](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Mod/MergeMultiVersion.cs#L240) `FolderGroupingLimitTranspiler` 只匹配 `code[i].Calls(get_Count) && code[i+1].opcode == OpCodes.Ldc_I4_8`。
   - 风险：仅覆盖 `ldc.i4.8`（即 C# 字面量 `8` 的常态编码）。若某编译版本把 `8` 编成 `ldc.i4.s 8` / `ldc.i4 8`，则**静默不替换**（`replaced==0`，仅 DebugMode 才打印）。
   - ✅ **已修复**：Transpiler 已扩展匹配三种编码形态，`replaced==0` 时 Warn（不再静默）。双 IL 实测仍建议执行。
2. **`FfmpegPathOverride.SetBackingField` 的 initonly 分支为空操作** — [FfmpegPathOverride.cs:77-81](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/FfmpegPathOverride.cs#L77-L81)。
   - `if (field == null || field.IsInitOnly) { if (field == null) return false; }` —— 当字段为 `initonly` 时**不拦截**，直接落到 `field.SetValue`；.NET Core 下对 initonly 字段写入会抛 `FieldAccessException`，被 catch 后返回 false。**注释所称「走反射去除 initonly 标记」并未实现**，属误导性死代码。
   - ✅ **已修复**：移除误导性 initonly 分支，直接 `SetValue`（.NET Core 反射写 initonly 字段有效，失败落入 catch）。
3. **`FfmpegPathOverride` 缺 PRO 的路径校验语义** — PRO 有 `CustomProbePathNotExist` 之类校验；本地仅在 `Apply` 时 `File.Exists` 跳过，**配置保存时不报错**，用户可能长期以为生效。✅ **已修复**：`MediaInfoExtractOptionsStore.OnFileSaving` 保存期校验自定义路径，不存在即 Warn。
4. **MovieDb 无版本分档** — [MetadataEnhanceOptions.cs:247-248](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MetadataEnhanceOptions.cs#L247-L248) 只 `Any(a => a.GetName().Name == "MovieDb")`。PRO 已适配 MovieDb 1.9.9.0（v3.0.0.55）。若 MovieDb 内部 API 变动，本地 patch 会静默失效。
5. **`MergeMultiVersion` 反射按方法名查找** — [MergeMultiVersion.cs:91](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Mod/MergeMultiVersion.cs#L91) `typeof(Series).GetMethod("AddLibrariesToPresentationUniqueKey", ...)`。Emby 若改名/改签名即失效（有 `missingComponents` 兜底，但功能静默降级）。
6. ✅ **已修复**：新增 `V4_10 = 41000` 分档。**顺带修复枚举数值序 bug**：原 `V4_9_2 = 4920` 小于 `V4_9_1_90 = 49190`，4.9.2+ 实例 `IsVersionAtLeast(V4_9_1_90)` 误判为 false——`V4_9_2` 已改 49200。
7. **`EmbyVersionCompatibility.cs` 已废弃但仍保留** — 头注释自述「已被 EmbyVersionAdapter 取代，保留仅为向后兼容」。属可清理技术债（非 bug）。

### 5.2 环境级适配注意（非代码 bug，但属「适配版本问题」）

- **sqlite3.dll**：Emby 4.9.1.1+ 需替换 `%appdata%\Emby-Server\system\sqlite3.dll` 为自编译版，否则中文搜索**静默失效**。
- **libsimple 分词器**：x64 用 0.7.1、**arm64 锁定 0.5.0**（上游 0.7.1 需 glibc ≥2.38），属**有意决策**，不是欠账（红线 6）。
- **目标 Emby 版本**：csproj 写 `mediabrowser.server.core 4.9.1.90`、注释「支持 4.9.1.0 及更高」；部署目录默认 `D:\下载\embyserver-win-x64-4.10.1.0`。**4.10.x 未在 csproj 里被显式验证**。
- **PRO 与本地架构差异**：PRO 用 `netcoreapp6.0` + `BlurayMounter` 等强依赖；本地坚持 `net6.0` + 无占位 dll（红线 4）。ISO 若要做，需**自带**能力而不是引 PRO 依赖。

---

## 六、修订后的阶段计划

### Phase 1 · 收尾（低风险，可立即做）
1. `ClearEmbeddedMetadata` 开关 + patch。
2. 搜索词繁→简转换接入搜索入口（复用已有 `CHTCHSConv`）。
3. 多版本排序/美化的**细分偏好组**补完（与 `EnableMultiVersionEnhance` 合并）。
4. `EnableAutomaticIntroScraping`（片头自动刮削开关）。
5. `RatingUpdateLookbackDays`。
6. **风险修复**：5.1 的 #1/#2/#3（Transpiler 形态、initonly 死代码、路径校验）。

### Phase 2 · UI / 前端注入
1. `OptimizeMediaItemOrder` + `OptimizeItemOrderPreferences`（四选）。
2. `ShowCollectionsFromSeasons`。
3. `EnforceUserPreferences` + `EnforceUserPreferencesScope`（**新开关，不与 `EnforceLibraryOrder` 合并**）。
4. JS 注入套件按需拆分（`GetIndicatorJs`/`GetItemJs`/`GetListJs`）。
5. `LocalDiscoveryUrl`（先探明 4.9/4.10 内部多点发现结构）。

### Phase 3 · 版本适配与数据源收尾
1. **MovieDb 版本分档**（对齐 PRO 的 ≤1.9.6 / 1.9.9.0 判定），修正 {5.1 #4}。
2. **HDR Vivid 检测**（`MultiVersionService` 画质分级与 `strmassistant.js` 徽标）。
3. 仪表盘搜索索引重建对齐（核对 `ScheduleIndexRebuildButton` 与 PRO 差异）。
4. `MetadataRefreshPreferences`/`PersonRefreshPreferences` 多选化（形态对齐 PRO）。
5. `EmbyVersionAdapter` 分档细化到 4.10.x（为 P4 铺路）。

### Phase 4 · 重型能力（每项独立立项，按可行度排序）
1. **本地 TMDB 缓存源**（价值最高）——先做**只读缓存**（限流/断网时可用），再谈失效策略。
2. **分布式提取**——路线选择：① 保持 rffmpeg（插件侧已通）② 补 `SyncMediaInfo` 端点。**先决策路线再动手**。
3. **音乐持久化**——`PersistMediaInfoTask` 扩音频分支。
4. **ISO/BDMV 提取**——**须先移除[LibraryApi.cs:61/83](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L61) 的硬编码排除**，再补自建 runner + 分段探测。
5. **跨库多版本进度**——**放最后**（数据面高风险）。

---

## 七、工程红线（沿用 v2，均未失效）

1. **资源名下划线坑**：MSBuild 把嵌入资源路径 `-` 转 `_`（`linux_arm64`），须核对 `GetManifestResourceStream` 查找名。
2. **ILRepack 参数同步**：[StrmAssistant.csproj:81](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/StrmAssistant.csproj#L81) 硬编码合并 `0Harmony`/`ChineseConverter`/`TinyPinyin`，新增依赖必须同步，否则运行时缺 dll。
3. **resx 三语同步**：`Resources.resx`/`.zh.resx`/`.zh-hant.resx` + `Resources.Designer.cs` 必须同步（本轮新增项已按此执行）。
4. **技术栈约束**：net6.0 下统一 `IHttpClient` + `IJsonSerializer`，不引 `HttpClient`/`System.Text.Json`；**不引入 PRO 的 `BlurayMounter` 等强依赖**。
5. **写回冲突**：豆瓣/本地 TMDB 一律「仅补空 + 字段级合并」。
6. **arm64 libsimple 锁 0.5.0**：有意决策。
7. **勿动清单（本地领先 PRO）**：`EnforceLibraryOrder`、`AutoMarkWatchedExcludeFavorites`、运维状态区（`EnableMemoryAutoRestart`/`EnableMemoryCleanup`/`RuntimeStatusList`）、`DirectLinkOptions`、`Core/` 版本适配层。

---

## 八、验收基线（每 Phase 通用）

1. `dotnet build` 无错误；ILRepack 产物自动部署到 `%APPDATA%\Emby-Server\programdata\plugins`。
2. x64 与 arm64 双端启动日志无 `Harmony not implemented` / `GLIBC not found`；`RuntimeStatusList` 各模块状态正常。
3. 新增配置项在 Emby 后台可见可改、重启后持久化，三语资源无缺。
4. 逐项回归：改配置 → 重启 → 验证行为 → 回滚配置 → 验证恢复。
5. **涉及 patch/Transpiler 的改动**，须在 4.9.1.90 与 4.10.x 两个 Emby 版本上各跑一次。
