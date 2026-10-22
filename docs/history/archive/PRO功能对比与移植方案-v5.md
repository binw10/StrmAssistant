# StrmAssistant 社区版 与 PRO 功能对比及移植方案（v5）

> 本地工程：`D:\Downloads\ai\StrmAssistant`（社区版底座 + 4.9.x / 4.10.x 适配，版本 **3.0.55**，`net6.0`）
> 参照对象：PRO `StrmAssistantPro.dll`，反编译自 `D:\Downloads\ai\StrmAssistant.Releases\Releases\StrmAssistantPro.dll`
> PRO 基线：**v3.0.0.55**（`AssemblyFileVersion=3.0.0.55`，`AssemblyInformationalVersion=3.0.0.55+3abcae82`）
> 方法：反编译 PRO 全量 Options / ScheduledTask / Web.Api / Web.Service / Provider，逐项与本地源码核对
> 日期：2026-10-15

> ⚠️ 说明：本仓库已有的 `PRO功能移植方案.md` / `-v4.md` / `-终版.md` 基于旧工作区（`StrmAssistant-myp015`、PRO 基线 .54）编写，其缺口矩阵已明显过期（例如其中的 `LocalTmdbCache`、`IsoExtraction`、`OptimizeItemOrder`、`EnforceUserPreferences`、`ShowCollectionsFromSeasons` 在本项目**均已落地**）。本文件是按当前源码重新核对的版本，可作为新的对比基线。

---

## 〇、基线对照

| 项 | PRO | 社区版（本项目） |
|---|---|---|
| 版本 | 3.0.0.55 | 3.0.55 |
| 目标框架 | `net6.0` | `net6.0` |
| 补丁技术 | MonoMod（`MonoMod.Core` + `Mono.Cecil`） | Lib.Harmony（`0Harmony`） |
| ISO/BDMV | 依赖 `BlurayMounter` + `DiscUtils.Udf` | 自研 `IsoExtraction` / `IsoMounter` |
| 其它强依赖 | `Portable.Licensing`（授权）、`SkiaSharp`、`ToolGood.Words`、`NfoMetadata`、`Tvdb`、`ServiceStack.Text` | `CHTCHSConv`、`TinyPinyin`、`SQLitePCL.pretty.core` |
| 配置页组织 | 分组：General / Network / Mod / Experience / IntroSkip(3 子页) / MediaInfo / Metadata(Build/Enhance/Refresh/Scrape) / Dashboard | 扁平单页聚合（11 个 Options 类） |

---

## 一、已对齐（PRO 功能，社区已完整移植）

| 模块 | 已对齐项 |
|---|---|
| 网络 | `EnableProxyServer`、`ProxyMode`、`ProxyServerUrl`、`LocalDiscoveryUrl`、`ShowProxyServerStatus`（`Options/NetworkOptions.cs`） |
| 体验增强 | `FolderVideoGroupingLimit`、`MergeMultiVersion`、`MergeMoviesPreference`/`MergeSeriesPreference`、`MetadataUpdateTrackedFields`、`EnhanceNotificationSystem`、`EnableDeepDelete`、`OptimizeSubtitle`、`SplitMoviesProgress`+`SplitMoviesButton`（`Options/ExperienceEnhanceOptions.cs`） |
| UI 功能 | `OptimizeMediaItemOrder`+`OptimizeItemOrderPreferences`、`ShowCollectionsFromSeasons`、`EnforceUserPreferencesScope`、`HidePersonPreference`、`ReplaceUnplayedWithTotal`、`ShowLibraryItemCount`、`ReverseLogLines`、`BeautifyMissingMetadata`、`EnhanceMissingEpisodes`、`NoBoxsetsAutoCreation`（`Options/UIFunctionOptions.cs`） |
| 片头片尾 | `EnableIntroSkip`、`UnlockIntroSkip`、`ClientScope`/`UserScope`、`LibraryScope`、`MarkerEnabledLibraryScope`、`ParallelMarkerComputation`、`Max*`/`Min*` 时长、`IntroDbApiKey`、`TidbApiKey`、`UnifiedIntroDbProviders`、黑名单（`Options/IntroSkipOptions.cs`） |
| 媒体信息 | `CustomProbePath`/`CustomEncoderPath`、`EnableImageCapture`、`ImageCapturePosition`、`ExclusiveExtract`+`ExclusiveControlFeatures`、`ImageExtractionTimeoutMultiplier`、`MediaInfoExtractionTimeoutMs`、`MediaInfoJsonRootFolder`、`PersistMediaInfoMode`、`IncludeExtra`、各类排除名单（`Options/MediaInfoExtractOptions.cs`） |
| 元数据 | `AltMovieDb*`、`ChineseMovieDb`、`ChineseTvdb`、`ClearTagline`、`MovieDbEpisodeGroup`、`LocalEpisodeGroup`、`PinyinSortName`、`PreferOriginalPoster`、`PreferTraditionalChinese`、`EnhanceMovieDbPerson`、`EnhanceNfoMetadata`、`FallbackLanguages`、`BlockNonFallbackLanguage`、`MdbListApiKey`、`RatingUpdateLookbackDays`、豆瓣 Cookie/评分、`MetadataCacheMode`+`RootFolder`+`OptionalImageTypes`（`Options/MetadataEnhanceOptions.cs`） |
| 运维/并发 | `CatchupMode`+`CatchupTaskScope`（4 任务）、`MaxConcurrentCount`、`Tier2MaxConcurrentCount`、`CooldownDurationSeconds`、内存自动重启/清理、`RuntimeStatusList`（`Options/GeneralOptions.cs`） |
| 搜索 | `EnhanceChineseSearch`、`ExcludeOriginalTitleFromSearch`、`SearchScope`、索引重建按钮（`Options/ModOptions.cs`） |

任务/服务侧也已基本对齐：`BuildDoubanCacheTask`、`ExtractIntroFingerprintTask`、`ExtractMediaInfoTask`、`ExtractVideoThumbnailTask`、`MergeMultiVersionTask`、`PersistMediaInfoTask`、`RefreshPersonTask`、`UpdatePluginTask` 等均在 `ScheduledTask/` 中存在。

---

## 二、社区缺口（PRO 有、社区尚未实现）

| # | PRO 功能 | PRO 证据 | 社区现状 |
|---|---|---|---|
| 1 | **跨库多版本播放进度** `LibraryScopePlayProgress`（按库分别记录多版本进度，仅剧集） | `UIFunctionOptions.cs` | 无 |
| 2 | **上集自动标已看** `MarkPreviousEpisodesPlayed`+`MarkPreviousEpisodesPlayedThreshold` | `UIFunctionOptions.cs` | 只有 `EnableAutoMarkWatched`（阈值自动标记当前集，语义不同） |
| 3 | 元数据刷新规则 **多选偏好** `MetadataRefreshPreferences`+`MetadataRefreshRule/List`、`PersonRefreshPreferences` | `MetadataRefreshOptions.cs` | 只做单值形态 `EpisodeRefreshScope`/`EpisodeRefreshLookbackDays`/`RefreshPersonMode` |
| 4 | 搜索调优 **多选** `SearchTuningPreferences`（DigitsAsTmdbId / ExcludeOriginalTitle / MixedChineseVariants / ShowMediaInfo / SuppressSearchSuggestions / TraditionalToSimplifiedChinese）+ `SearchIndexOption` | `ModOptions.cs` | 已拆分实现其中 3 项，无统一 `SearchTuningPreferences` 与 `SearchIndexOption` |
| 5 | **清除内嵌元数据** `ClearEmbeddedMetadata` | `MediaInfoExtractOptions.cs` | **已实现但常开**：`Mod/ExclusiveExtract.cs:64,204-233` 的 `ShouldCleanEmbeddedMetadata` + `GetMediaInfoPostfix` 已清除 `Name/Overview/PremiereDate/ProductionYear/Studios/Tags/Album*/Artists/Genres` 并剔除内嵌图片流；**已补独立开关** `MediaInfoExtractOptions.ClearEmbeddedMetadata`（默认 ON，与 PRO 一致），`GetMediaInfoPostfix` 受其控制（原判“无”为误判，已修正） |
| 6 | **片头自动刮削开关** `EnableAutomaticIntroScraping` | `IntroSkipScrapeOptions.cs` | **已实现**：`Options/IntroSkipOptions.cs` 新增独立开关（默认 **OFF**，与 PRO 对齐），刷新回调需 `EnableIntroDbScrape && EnableAutomaticIntroScraping` 同开才自动刮削 |
| 7 | **多版本美化细分** `BeautifyMultiVersion`、`MultiVersionDisplayPreference`、`MultiVersionReorderPreference`、`MergeMoviesScopeOption`/`MergeSeriesScopeOption` | `ExperienceEnhanceOptions.cs` / `MultipleVersionsOptions.cs` | 为 `EnableMultiVersionEnhance`+`MultiVersionDisplayOption`+`MultiVersionSortOption`，形态不同、粒度较粗 |
| 8 | **分布式提取写回** `POST /Items/SyncMediaInfo` + `MediaInfoService` | `Web.Api/SyncMediaInfo.cs`、`Web.Service/MediaInfoService.cs` | 无（薄侧靠 `CustomProbePath` 指向 rffmpeg wrapper，无写回端点） |
| 9 | **JS 注入套件拆分** `GetIndicatorJs`/`GetItemJs`/`GetListJs`/`GetListControllerJs`/`GetRefreshDialogJs`/`GetShortcutJs`/`GetFilterMenuHtml`/`GetLibraryOptionsEditorJs`/`GetLinkedItemsJs`/`GetMediaInfoJs` | `Web.Api/*` | 仅单入口 `Web/Api/GetStrmAssistantJs.cs` |
| 10 | `EnableAutomaticDecompression`（网络自动解压） | `NetworkOptions.cs` | 改为 `IgnoreCertificateValidation`（非等价） |
| 11 | `RestartServerTask`、`TriggerPopulateDoubanIdTask`、`ScanExternalTrackTask`（独立任务） | `ScheduledTask/*` | 为按钮式重启、外挂字幕用 `ScanExternalSubtitleTask` |
| 12 | Provider 侧 `BangumiExternalId`（番剧源）、`PersistMediaInfoProvider`、`IntroMarkerProtector`、`PersonService`+`DeletePerson` API | `StrmAssistant.Provider/*` | 均无 |
| 13 | `SuppressPluginUpdates`（屏蔽插件自动更新） | `ExperienceEnhanceOptions.cs` | **主动移除**（26.3.25 决策），属有意不移植 |

---

## 三、社区独有 / 领先 PRO 的功能

| 模块 | 社区独有项 |
|---|---|
| 影音直链 | `Options/DirectLinkOptions.cs`：`EnableStrmDirectRedirect`、`EnableLiveTvDirectRedirect`、`Follow302`、音/视频客户端黑名单 —— PRO 完全没有 |
| ISO/BDMV | 自研 `EnableIsoExtraction` + `Mod/IsoExtraction.cs` + `Common/IsoMounter.cs`（PRO 靠 `BlurayMounter` 依赖，无用户开关） |
| 运维状态区 | `EnableMemoryAutoRestart`+倒计时通知、`EnableMemoryCleanup`、`RuntimeStatusList`（比 PRO 的 `MemoryOptions`/`SystemInfoItemList` 更丰富） |
| 版本适配层 | `Core/` 全套：`EmbyVersionAdapter`/`EmbyApiVersion`、`FastReflection`、`HealthCheck`、`MemoryGuard`/`MemoryCleaner`、`PerformanceMonitor`/`PerformanceReporter`、`OptimizationAdvisor`、`ServiceLocator`、`ConflictDetector` —— PRO 无此层 |
| 播放会话监控 | `IntroSkip/PlaySessionMonitor.cs` 播放会话内实时片头/片尾标记 |
| 其它 | `ScrapeProxyWhitelist`、`IgnoreCertificateValidation`、`EnsureEpisode16x9`/`ForcePortraitPoster` 独立开关、`DefaultNewLibraryTmdb`、`DisableEpisodeImageScraping`/`OptimizeMovieDbEpisodeScraping`、`GetDoubanRatings`/`GetMultiVersionInfo` API、`DoubanService`/`MultiVersionService`、豆瓣辅助刮削细分（`DoubanCastChinese`/`DoubanPosterFallback`/`DoubanRatingOverride`） |

---

## 四、ISO/BDMV 专项对比

### 4.1 PRO 的做法：进程内读盘，跨平台、全程不挂载

反编译实证（`A----------\A----------.cs`）：

- 依赖 `BlurayMounter` + `DiscUtils.Udf/Core/Streams`（csproj 第 37、57-59 行）。
- **全文无任何 OS 挂载调用**（无 `Mount-DiskImage` / `Dismount`）。
- 用 Harmony **Transpiler 把 Emby 托管文件系统接口 `IFileSystem.OpenRead(path)` 替换为自定义 `OpenRead`**：命中时走 `new UdfReader(stream)` + `DiscFileSystem.OpenFile/GetFileInfo().OpenRead()`，即**让 Emby 自己对 ISO 内容的读取转由进程内 UDF 完成**（`58778-58787`、`58819-58823`、`58834-58917`）。
- 碟片结构/正片由 `BdInfoExaminer(BaseItem.FileSystem).GetDiscInfo(path)` 解析（`59260-59269`），并消费 `BlurayDiscInfo.PlaylistName`、`Chapters`、`MediaStreams`、`RunTimeTicks`、`Files`（`59170-59227`）；蓝光播放列表由 `TSPlaylistFile` / `BDROM.PlaylistFiles` 装载（`60229-60329`）——`BlurayDiscInfo` / `BDROM` / `TSPlaylistFile` 这些类型来自 `BlurayMounter`（内含 BDInfo 港口）。
- 处理 `BDROM` 类型时设置 `DirectoryRoot`（`58919-58956`）。
- 最后把 `MediaSourceInfo.Container` 置为 `"iso"`（`59634`）。
- Linux/Docker 下仍需 `CustomProbePath` 指向带 dvd-iso demuxer 的 ffprobe（PRO 选项原文 *Used for ISO media info extraction — Required on Linux*），因为最终媒体流探测仍由外部 ffprobe 完成。

> **结论修正**：PRO 不是"挂载后替换路径"，而是"**patch Emby 托管文件系统的读取路径 → 转为进程内 UDF**"。因此它不依赖任何 OS 挂载能力，天然适配 Docker/Linux；但 `UdfReader` 产出的是 `Stream` 而非 OS 文件路径，**这部分能力无法直接用于需要真实路径的外部 ffprobe** —— 这也是 Linux 下仍要自定义 ffprobe 的原因（见附录 B 的设计约束）。

### 4.2 社区版的做法：操作系统挂载，Windows 专用

- 无第三方依赖，靠 PowerShell 的 `Mount-DiskImage` 把 ISO 挂成虚拟光驱，再定位正片：**BDMV** 取 `BDMV\STREAM` 下最大 `.m2ts`，**DVD** 取 `VIDEO_TS\VTS_01_1.VOB`（`Common/IsoMounter.cs`）。
- 挂载成功后 **只替换 ffprobe 的输入路径**，不改 `MediaSourceInfo.Path`（`Mod/IsoExtraction.cs`）—— 播放路径与条目身份不受影响，比 PRO 更保守、更安全。
- `IsSupported` 判定为「Windows 且存在 `Mount-DiskImage`」，否则**自动跳过**。
- 选项默认 OFF；OFF 时 ISO 被强制排除出提取，ON 时才放行并"交由自定义 ffprobe（含 dvd-iso demuxer）处理"（`Common/LibraryApi.cs`、`Options/MediaInfoExtractOptions.cs`）。

### 4.3 环境对照表

| 维度 | PRO | 你的社区版 |
|---|---|---|
| 适用平台 | Windows / Linux / **Docker** / macOS | **仅 Windows**（其余自动跳过） |
| 机制 | 进程内 `DiscUtils.Udf` 读 UDF + `BlurayMounter` 解析碟片 | PowerShell `Mount-DiskImage` 挂虚拟光驱 |
| Docker | ✅ 可用 | ❌ 不可用（无 PowerShell、无挂载权限） |
| Linux | ✅，需自定义 ffprobe | ⚠️ 仅靠自定义 ffprobe 直读，无挂载 |
| 蓝光正片选择 | 按 playlist 解析，准确 | 取最大 `.m2ts`，**多 m2ts / 无缝分支可能选错** |
| 默认 | 内建（无开关） | 默认 OFF，ISO 默认被排除 |
| 副作用 | 改 Container 为 iso | 只改探测路径，更安全 |

**一句话**：PRO 的 ISO/BDMV 是为 **Linux/Docker** 设计的（进程内读盘）；社区版是 **Windows 专属挂载**方案，Docker/Linux 用户实际用不了，且蓝光正片选择精度不足。

---

## 五、移植方案

### 前置约束（工程红线）

1. 技术栈 `net6.0`，不引 `BlurayMounter` 等 PRO 私有强依赖。
2. 新增依赖必须同步 `StrmAssistant/StrmAssistant.csproj` 第 77-81 行的 ILRepack 参数（否则运行时缺 dll）。
3. `Resources.resx` / `.zh.resx` / `.zh-hant.resx` + `Resources.Designer.cs` 三语必须同步。
4. 写回类改动（豆瓣/缓存）一律「仅补空 + 字段级合并」。
5. 枚举选项本地化不得依赖 Emby 4.10 的 `EnumExtensions.GetDescription`（需用基类匹配的 DescriptionAttribute 查找）。
6. 不引 GPL/私有依赖：`BlurayMounter`（PRO 私有）与 `BDINFO`（GPL）均不引入，蓝光 playlist 解析自研；新增开源依赖须为 MIT/BSD/Apache（如 `DiscUtils`）。

### P0 · 高价值低风险（本迭代先落）

| 项 | 做法 | 说明 |
|---|---|---|
| **P0-1. `ClearEmbeddedMetadata`**（✅ 已完成） | 社区本来就有清除逻辑（`ExclusiveExtract.GetMediaInfoPostfix`，且**常开**），本项改为**提为独立开关**：`Options/MediaInfoExtractOptions.cs` 新增 `ClearEmbeddedMetadata`（默认 ON，保持现状），并让 `GetMediaInfoPostfix` 受其控制 | 无新依赖、体量小，用来先打通「构建→ILRepack→部署→Emby 加载」全链路 |
| **P0-2. `EnableAutomaticIntroScraping`**（✅ 已完成） | 片头页加独立开关，显式控制“新内容自动刮削”：`Options/IntroSkipOptions.cs` 新增 `EnableAutomaticIntroScraping`（默认 **OFF**，与 PRO 对齐；PRO 资源键 `..._Default_is_OFF_` 反编译实证）；`Plugin.cs` 刷新回调改为需 `EnableIntroDbScrape && EnableAutomaticIntroScraping` 同开 | 三份 resx + `Resources.Designer.cs` 同步；默认 OFF 偏保守，避免默认拖慢媒体库扫描 |
| **P0-3. ISO/BDMV 跨平台化 POC**（✅ 已完成） | 验证 `DiscUtils.Udf` 在 net6.0 可用 + `UdfReader` 打开样例 ISO + 自研 MPLS 解析选正片 | **只做验证、未改 csproj**。成果（独立 POC 工程，输出：net6.0 / `DiscUtils.Udf 0.16.13` 正常还原编译运行；`UdfReader` 构造 `(Stream data)`；真实 UDF 镜像（IMAPI2FS 生成、`Mount-DiskImage` 自检为 UDF）读取成功，能枚举 `BDMV\PLAYLIST`、`BDMV\STREAM`；按 libbluray `bdnav/mpls_parse.c` 结构自研 MPLS 解析（文件+8=PlayList 地址；PlayItem 体偏移 in=12/out=16，45kHz）选中最长正片）。**关键结论**：路径分隔符用 `\`；依赖仅 `DiscUtils.Core/Streams/Udf/Iso9660` 四个 dll，net6.0 **无需 System.Memory 等垫片**。⚠️**缺口**：「多分支/无缝衔接真碟选对正片」用合成盘无法实证，需真碟样片，已降级到 **P1-1 真碟补验** |

### P1 · 高价值中风险（下一迭代）

| 项 | 做法 |
|---|---|
| **P1-1. ISO/BDMV 全量落地**（✅ 已完成，保守方案，见「附录 B」） | 引入 `DiscUtils.Udf/Core/Streams/Iso9660`（MIT）+ ILRepack 合并（已实证合并产物**无 DiscUtils 程序集外部引用**）；自研轻量 MPLS 解析（`BDMV/PLAYLIST/*.mpls` → 按 PlayItem 时长**求和**取最长 playlist → 最长 PlayItem 的 5 位号 → `STREAM/<5 位号>.m2ts`）；DVD 回退 `VTS_01_1.VOB`/最大 VOB；**不引 BDInfo/BlurayMounter**；Windows 挂载卷上用该解析结果选正片（失败回退最大 m2ts），Linux/Docker 进程内 UDF 选片仅作日志校验、`.iso` 走 `CustomProbePath`。**本轮不做** PRO 的 `IFileSystem.OpenRead` Transpiler（社区无进程内读盘消费方，且属最高风险 patch 类），如需完全对齐后续单独立项 |
| **P1-2. `MetadataRefreshPreferences` / `PersonRefreshPreferences` 多选化** | 把单值 `EpisodeRefreshScope` / `RefreshPersonMode` 升级为 PRO 的多选偏好 + 规则（`MetadataRefreshRule`），保持旧配置可读 |
| **P1-3. `SearchTuningPreferences` 多选 + `SearchIndexOption`** | 把已散落的 `EnhanceChineseSearchRestore` / `ExcludeOriginalTitleFromSearch` / `BlockSearchRecommend` 归一为一个多选偏好（PRO 6 项：DigitsAsTmdbId / ExcludeOriginalTitle / MixedChineseVariants / ShowMediaInfo / SuppressSearchSuggestions / TraditionalToSimplifiedChinese） |
| **P1-4. `MarkPreviousEpisodesPlayed` + 阈值** | 与现有 `EnableAutoMarkWatched` 并列/合并（语义不同：PRO 是"看完当前集把前几集标已看"，社区是"阈值自动标当前集"） |

### P2 · 有需求再做

| 项 | 做法 |
|---|---|
| **P2-1. JS 注入套件拆分** | 把单入口 `Web/Api/GetStrmAssistantJs.cs` 拆成 `GetIndicatorJs`/`GetItemJs`/`GetListJs`/`GetRefreshDialogJs` 等按需注入，性能更好 |
| **P2-2. `SyncMediaInfo` 分布式写回** | 补 `POST /Items/SyncMediaInfo` 端点 + 写回服务；社区已有 `CustomProbePath`→rffmpeg 路线，**先决策走哪条路**再动手 |
| **P2-3. `EnableAutomaticDecompression` / `GenerateBackdrop` 等小项** | 网络自动解压、图像后处理补充 |

### P3 · 高风险，放最后

| 项 | 风险 |
|---|---|
| **P3-1. `LibraryScopePlayProgress`（跨库多版本进度）** | 需改 `PresentationUniqueKey` 计算链路，直接影响播放进度/收藏键，数据面高风险，建议单独立项、充分回归 |

### 明确不建议移植

- `SuppressPluginUpdates`：社区 26.3.25 已**主动移除**（刻意决策）。
- PRO 的 `Activation / License`（Portable.Licensing）：PRO 私有商业逻辑，社区版无意义。
- 对 `BlurayMounter` 的直接依赖：违反社区"无强依赖"红线，用 A1 的 DiscUtils 方案替代。

### 执行顺序（P0 → P3）

1. **P0-1 `ClearEmbeddedMetadata`（✅ 已完成）→ P0-2 `EnableAutomaticIntroScraping`（✅ 已完成）**：两项均无新依赖、体量小，用来先打通「构建 → ILRepack → 部署 → Emby 加载验证」全链路。产物已部署至 `D:\Downloads\embyserver\programdata\plugins\StrmAssistant.dll`。**P0-2 默认值经 PRO 反编译证实为 OFF，已对齐**；三份 resx + `Resources.Designer.cs` 同步（缺 Designer 属性会导致配置页 500/空白）。**说明**：仓库内无 `CHANGELOG.md`，且版本号刻意与 PRO 基线对齐（`3.0.55`），故本轮未递增版本号——是否递增待定。
2. **P0-3 ISO/BDMV POC（✅ 已完成）**：`DiscUtils.Udf` 在 net6.0 可用、`UdfReader` 打开真实 UDF 镜像、自研 MPLS 解析选最长正片均已跑通（合成盘）；**真碟多分支正片验证降级到 P1-1**。
3. **P1-1 ISO/BDMV 全量落地（✅ 已完成，保守方案）**：按「附录 B」落地——csproj 加 `DiscUtils.Udf 0.16.13` + ILRepack 追加 4 个 dll（构建实证：合并产物含 306 个 DiscUtils 类型、**无 DiscUtils.* 程序集引用**，运行时无需外部 dll；`System.Memory`/`System.Runtime.CompilerServices.Unsafe` 由 Emby 自带运行时提供）；新增 `Common/UdfTitleResolver.cs`（进程内 UDF + 已挂载卷双实现共用同一选片逻辑）；`Common/IsoMounter.cs` 拆出 `ResolveMountedTitle`（playlist 优先）与 `ResolveInProcessTitle`；`Mod/IsoExtraction.cs` 与 `MediaInfoExtractOptionsStore` 的 Patch 门控解绑 `IsSupported`（Linux/Docker 无挂载也可启用）；`EnableIsoExtraction` 描述改跨平台文案（新键 + 三语 resx + Designer 同步）。**待真碟补验**：多分支/多 m2ts 蓝光 ISO 选片正确性；Linux/Docker 下自定义 ffprobe 能否直读蓝光 ISO（ffmpeg 的 dvd-iso demuxer 只面向 DVD，若不能则需启动附录 B 的方案 b：临时解出 m2ts）。**不做** `IFileSystem.OpenRead` Transpiler。
4. **P1-2 ~ P1-4**（配置形态对齐，注意旧配置兼容 + 三语 resx）。
5. **P2**（按用户反馈）。
6. **P3-1**（单独风险评估后立项）。

---

## 六、验收基线（每 Phase 通用）

1. `dotnet build` 无错误；ILRepack 合并产物自动部署到 **`D:\Downloads\embyserver\programdata\plugins`**（csproj `StrmAssistantPluginOutputDir`，可用 `/p:StrmAssistantPluginOutputDir=...` 覆盖）；测试服务器 Emby 版本 **4.10.1.0 (x64)**。
2. x64 与 arm64 双端启动日志无 `Harmony not implemented` / `GLIBC not found`；`RuntimeStatusList` 各模块状态正常。
3. 新增配置项在 Emby 后台可见可改、重启后持久化，三语资源无缺。
4. 逐项回归：改配置 → 重启 → 验证行为 → 回滚配置 → 验证恢复。
5. 涉及 patch / Transpiler 的改动，须在 4.9.1.90 与 4.10.x 两个 Emby 版本上各跑一次。
6. ISO/BDMV 改动额外要求：Windows 挂载路径与 Linux/Docker 进程内读取两条路径均验证；蓝光多 m2ts 碟片正片选择正确。

---

## 附录 A · 证据与置信度（回应"是否基于代码对比"）

本文所有结论均来自**实际代码**：PRO 侧用 `ilspycmd` 反编译 `StrmAssistantPro.dll`（145 个文件）；社区侧为本地 git 源码。逐条证据如下：

| 结论 | 证据（可复现） | 等级 |
|---|---|---|
| PRO 基线 v3.0.0.55 | 反编译 `Properties/AssemblyInfo.cs` | 反编译实证 |
| PRO 依赖 `DiscUtils.Udf/Core/Streams` + `BlurayMounter` | `StrmAssistantPro.csproj:37,57-59` | 反编译实证 |
| PRO 用 Transpiler patch `IFileSystem.OpenRead` → 进程内 UDF，**不挂载** | `A----------.cs:58778-58787, 58819-58823, 58834-58917, 58919-58956`；全文无 `Mount-DiskImage`/`Dismount` | 反编译实证 |
| PRO 蓝光 playlist 解析 | `A----------.cs:59170-59227`（`BlurayDiscInfo.PlaylistName/Chapters/MediaStreams`）、`59260-59269`（`BdInfoExaminer.GetDiscInfo`）、`60229-60329`（`TSPlaylistFile`/`BDROM.PlaylistFiles`） | 反编译实证 |
| PRO 置 `Container="iso"` | `A----------.cs:59634` | 反编译实证 |
| PRO 无 ISO 用户开关（默认内建） | PRO Options 中 grep `EnableIsoExtraction` = 0 命中 | 反编译实证 |
| Linux 需自定义 ffprobe | PRO 选项说明原文 *Required on Linux* | 反编译实证（文本） |
| 社区 ISO：Windows 挂载、非 Windows 跳过 | `Common/IsoMounter.cs:43-62,105-208`、`Mod/IsoExtraction.cs:30-34,74-103`、`Plugin.cs:722` | 源码实证 |
| 「已对齐」各模块 | 社区 `Options/*.cs` 存在属性 + PRO Options 属性比对 | 源码实证（属性级） |
| 「社区缺口」各条 | 社区源码 grep 关键词 0 命中 + PRO 侧属性存在 | 源码实证（关键词级） |

**置信度分级**：
- **配置面结论（一、二、三章）——高**：基于属性名与关键词的直接比对，可点开文件复核；唯一弱点是"改名但功能等价"的项可能被误列入缺口，已对这类项标注"形态不同"。
- **PRO 运行时行为（第四章）——高**：来自反编译代码直读，附行号；其中"PRO 用 playlist 选正片"为实证（`TSPlaylistFile`/`PlaylistName`），非猜测。
- **本文原稿曾有一处表述不严谨**：曾称"`UdfReader` 仅用于探测路径""PRO 挂载后替换路径"，经复核为**错误推断**，已在 §4.1 与 A1 修正为"进程内 patch `OpenRead` + UDF 读流"。
- **个别"语义等价"判断（如 `MarkPreviousEpisodesPlayed` vs `AutoMarkWatched`）——中**：属行为语义推断，已在正文标注差异。

---

## 附录 B · A1「ISO/BDMV 跨平台化」文件级实施设计

**目标**：Linux/Docker 也能提取 ISO 媒体信息；蓝光正片按 playlist 正确选择；不引 GPL/私有件；不破坏现有"只改探测输入、不改 `MediaSourceInfo.Path`"的安全约束。

**依赖变更**
- 新增 NuGet：`DiscUtils.Udf`（含 `DiscUtils.Core` / `DiscUtils.Streams`，MIT）。
- `StrmAssistant/StrmAssistant.csproj`：ILRepack 参数追加 `DiscUtils.Core.dll DiscUtils.Streams.dll DiscUtils.Udf.dll DiscUtils.Iso9660.dll`（POC 实证 `DiscUtils.Udf` 会传递引入 `DiscUtils.Iso9660`；net6.0 下无 System.Memory 等垫片）；`PackageReference` 增加对应包。
- **不引入** `BDInfo` / `BlurayMounter`。

**改动点**

1. `StrmAssistant/Common/UdfTitleResolver.cs`（新增）
   - `Resolve(isoPath, out isBluray)`：用 `DiscUtils.Udf.UdfReader` 打开 ISO（`UdfReader(new FileStream(...))`）。
   - 蓝光：枚举 `BDMV/PLAYLIST/*.mpls` → 轻量 MPLS 解析（`MPLS####` 头 → `PlayList` → `PlayItem` 列表含 `in_time`/`out_time`，45kHz tick）→ 取时长最长 PlayItem → 映射 `BDMV/STREAM/<5 位号>.m2ts`。
   - DVD：`VIDEO_TS` → `VTS_01_1.VOB`，缺失则最大 `.VOB`。
   - 返回相对路径（不返回 OS 路径），并给出选中正片时长用于日志。

2. `StrmAssistant/Common/IsoMounter.cs`
   - 保留 Windows `Mount-DiskImage` 全流程（`Mount`/`ResolveMainTitle`/`SweepIdle`/`DismountAll`）。
   - 新增 `UdfReadSupported`（跨平台恒 true，`DiscUtils` 可加载即可）。
   - `GetProbePath` 增加回退序：Windows 挂载成功 → 用挂载正片；挂载失败或非 Windows → 记录 `UdfTitleResolver` 选出的正片供探测/日志。

3. `StrmAssistant/Mod/IsoExtraction.cs`
   - 保持"仅替换 `inputPath`、绝不动 `primaryPath`"的约束不变。
   - 平台分支：
     - **Windows**：沿用挂载路径（现状）；
     - **Linux/Docker**：`inputPath` 保持 `.iso`，交由 `CustomProbePath` 指向的带 `dvd-iso` demuxer 的 ffprobe 直读；`UdfTitleResolver` 用于正片发现与日志校验。
   - 方案 b（可选增强，非必需）：把选中正片从 UDF 解出到 `%TEMP%/strm-iso/<hash>.m2ts`，探测/缩略图走临时文件，空闲清理（与 `IsoMounter` 清扫器统一）。

4. `StrmAssistant/Options/MediaInfoExtractOptions.cs` + 三语 `Resources*.resx`
   - `EnableIsoExtraction` 文案更新为"跨平台：Windows 挂载 / Linux·Docker 进程内 UDF + 自定义 ffprobe"；en / zh / zh-hant 三语同步。

5. `StrmAssistant/Plugin.cs`
   - `OnUninstalling` 保留 `IsoMounter.DismountAll()`（第 722 行）；如启用方案 b，追加临时目录清理。

**验收矩阵**

| 维度 | 用例 | 断言 |
|---|---|---|
| 平台 | Windows x64/arm64 × Linux·Docker x64/arm64 | 均能取到媒体信息 |
| 蓝光 | 单 playlist ISO / 多 m2ts 无缝分支 ISO | 选中正片正确（对比时长） |
| DVD | `VTS_01_1` 存在 / 缺失 | 分别取 VTS_01_1 / 最大 VOB |
| 异常 | 含 AACS 的蓝光 ISO、损坏 ISO | 静默回退原路径，不报错中断 |
| 安全 | 任意 ISO | `MediaSourceInfo.Path` 未被改写 |
| 生命周期 | 提取后 / 插件卸载 | 无残留挂载点、无残留临时文件 |

**落地说明（实际实现 vs 本设计）**

- 入口为 `UdfTitleResolver.Resolve(string isoPath)`，返回 `TitleInfo`（`IsBluray` / `RelativePath` / `SourceName` / `DurationSeconds`），语义等价于设计中的 `Resolve(isoPath, out isBluray)`。
- 选片粒度按「playlist 内**所有** PlayItem 时长**求和**」取最长 playlist（不是最长单 PlayItem、也不是最大单 m2ts），多 m2ts 分段与无缝分支才不会选错；再取该 playlist 内最长 PlayItem 的 5 位号映射 `STREAM/<5 位号>.m2ts`（MPLS `Clip_Information_file_name` 与 m2ts 同号，**无需**解析 STN 流表）。
- 双来源共用同一套选片逻辑：`IDiscFs` 抽象由进程内 `UdfDiscFs`（`UdfReader`）与已挂载卷 `LocalDiscFs` 各实现一份。
- Windows：`IsoMounter.ResolveMountedTitle` 先按 playlist 选片并映射到盘符根，找不到再回退「最大 .m2ts / `VTS_01_1.VOB`」。
- Linux/Docker：`IsoMounter.ResolveInProcessTitle` 只做选片记录（Info 日志），`inputPath` 保持 `.iso`，由 `CustomProbePath` 的自定义 ffprobe 直读。
- **本轮未做**：PRO 的 `IFileSystem.OpenRead` Transpiler；方案 b（临时解出 m2ts 到 `%TEMP%`）——先等真碟实证 Linux 侧 ffprobe 能否直读蓝光 ISO（ffmpeg 的 `dvd-iso` demuxer 只面向 DVD/`VIDEO_TS`，蓝光 ISO 大概率读不了，届时方案 b 转为必做）。
- **待补验**：多分支 / 多 m2ts 真碟选片正确性；含 AACS 的蓝光 ISO 与损坏 ISO 的静默回退；x64/arm64 双端加载；Windows 与 Linux/Docker 双路径。
