> 本地工程：`D:\下载\AI\StrmAssistant-myp015`（分支 `myp015`，HEAD `054c1ff`；插件 StrmAssistant Plus，版本 **3.0.54**，net6.0）
> 参照对象：PRO **v3.0.0.53**（`_pro_tmp/decompiled` 反编译，`AssemblyVersion=3.0.0.53`）；GitHub Releases 最新 **v3.0.0.55**（2026-10-15）
> 本版为 **v4**，替代 v3（`PRO功能移植方案-终版.md`）；日期 2026-10-15
> 方法论：**以代码为准**。PRO changelog 全量（v3.0.0.23→v3.0.0.55）逐条反查本地源码；不采信旧文档结论，逐项 grep 复核。

---

## 〇、本轮新增的两个关键事实（先看这里）

1. **PRO 更新日志的完整来源已定位**：插件运行时从
   `https://api.github.com/repos/sjtuross/StrmAssistant.Releases/releases/latest` 拉取 release body（见 `strings.txt`）。本轮已把 **v3.0.0.23 → v3.0.0.55 全部条目**拉齐（v3 文档只覆盖到 .53/.55 两条）。其中 **.54/.33/.38 无 Release**（.34/.35/.36 有）。
2. **发现一个真实的「适配版本问题」bug**（详见第四节 BUG-1）：本地 `EmbyApiVersion` 枚举的**数值序被写反**，4.10.x 的枚举值（41000）**小于** 4.9.1.90（49190）与 4.9.2（49200），且 `V4_10` 与 `V4_10_0` **重复取值 41000**。当前恰好未引爆（唯一消费点是 `IsVersionAtLeast(V4_9_1)`），但属高危埋雷。

---

## 一、用户猜测的「P4 五项硬项」——代码级逐项答复

**结论：5 项中，4 项确实未做（本地 TMDB 源 / ISO 提取 / 音乐持久化 / 跨库进度）；「分布式提取」只做到了「rffmpeg 形态」的一半，PRO 的 `SyncMediaInfo` API 未移植。**

| # | 项 | 是否已做 | 代码证据（本地） | PRO 侧证据 | 落地难度 |
|---|---|---|---|---|---|
| 1 | **本地 TMDB 刮削源/缓存** | ❌ **未做** | 全库 grep `MetadataCacheMode`/`MetadataCacheRootFolder`/`MetadataCacheOptionalImageTypes`/`ClearRecentTmdbCacheOnStartup` **零命中**；`Mod/ChineseMovieDb.cs` 只做中文元数据补丁，`_cacheTime` 字段补丁非本地源 | `MetadataEnhanceOptions.cs`（`MetadataCacheMode` + `MetadataCacheOption_None/PreferCacheThenOnline/PreferOnlineWithCache/UseOverrideCacheOnly` + `MetadataCacheRootFolder` 校验 `InitFailed/NotAllowed/NotExist`）；changelog .40「本地 tmdb 支持导入自定义外部id」、.41「不再要求图像刮削器排首位」、.51「支持合并补丁机制」 | **大**：缓存目录结构/失效策略 + patch MovieDb provider HTTP/序列化层；豆瓣落盘缓存范式可复用 |
| 2 | **ISO/BDMV 提取（含 DVD ISO、http ISO、`.iso.strm`）** | ❌ **未做，且被主动排除** | [LibraryApi.cs:62](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L62) `.Concat(new[] { MediaContainers.Iso })`、[:84](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L84) `.Concat(MediaContainers.Iso.GetAliases())` 硬编码兜底排除；无 BlurayMounter、无 mpls/BDMV 解析、无 `.iso.strm` | changelog .42「自定义ffmpeg支持ISO视频截图和章节图」、.48 「http链接ISO媒体信息提取」、.49「`.iso.strm` 命名标准 + http ISO 截图章节图」、.50「提升自动识别http iso」、.53「**支持 DVD ISO**媒体信息提取与视频截图」；资源键 `CustomProbePath_Used_for_ISO_media_info_extraction__Required_on_Linux` | **大**：需自建 ProcessRunner + 静态 ffprobe 分段探测；挂载基建已有（[LibraryApi.cs:935](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L935) `IMediaMountManager` 反射挂载） |
| 3 | **音乐持久化** | ❌ **未做** | [PersistMediaInfoTask.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/ScheduledTask/PersistMediaInfoTask.cs) 全文件无 `Audio`/`Album`/`Artist`/内嵌封面分支（仅视频媒体信息） | changelog .35「无法提取音乐内嵌图片」、.42「**SyncMediaInfo 接口支持音频**」；PRO 有 `PersistMediaInfoProvider.cs` | **中**：Album/Artist 序列化 DTO + 写回白名单（音乐库刷新频繁，冲突面大） |
| 4 | **分布式提取** | 🔧 **仅「配置形态」一半** | 插件侧**无** `SyncMediaInfo` API（grep 零命中）；本地路线是 `CustomProbePath` 指向 rffmpeg wrapper（[MediaInfoExtractOptions.cs:157](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MediaInfoExtractOptions.cs#L157)，描述里明确 "supports rffmpeg wrapper"） | changelog .40「分布式媒体信息提取」（`POST /Items/SyncMediaInfo`）、.46「**分布式片头声纹提取**」；`SyncMediaInfo.cs` + `MediaInfoService.cs` 写回 | **小-中**：rffmpeg 路线插件侧基本已通；若要「集中共享/中央调度」再补 API 端点 |
| 5 | **跨库多版本进度** | ❌ **未做** | `LibraryScopePlayProgress` grep 零命中；`PresentationUniqueKey` 相关补丁只服务于多版本合并 | `UIFunctionOptions_LibraryScopePlayProgress_Separate_Play_Progress_by_Library`（"Tracks play progress for multi versions separately per library (Episodes only)"）；changelog .31「各媒体库内记录播放进度扩展支持收藏」 | **大（高风险）**：需改 `PresentationUniqueKey` 计算链路，影响播放进度/收藏键 |

---

## 二、PRO 更新日志 × 本地实现 全量核对（v3.0.0.23 → v3.0.0.55）

图例：✅ 已覆盖（含等价实现）｜🔧 部分/形态不同｜❌ 缺口｜⬛ 本地领先 PRO

### 2.1 已覆盖（本轮代码级确认，无需再动）

| changelog 条目 | 版本 | 本地落点 |
|---|---|---|
| 代理支持刮削白名单 | .23 | `NetworkOptions.ProxyMode(Global/ScrapeOnly)` + `ScrapeProxyWhitelist` |
| 媒体元数据字段变更通知 | .23 | `MetadataUpdateTrackedFields` + `CustomNotifications` |
| 隐藏演职人员"非中文名人员" | .27 | `HidePersonOption.NonChinese` |
| 新建媒体库默认 TMDB | .26 | `Mod/DefaultTmdbLibrary.cs` |
| 隐藏设置可调整 ffprobe 基准超时 | .26 | `MediaInfoExtractionTimeoutMs` |
| 清除媒体信息（右键） | .31 | `Web/Api/ClearMediaInfo.cs` |
| 剧集组 json 扩展自定义季简介/首播 | .31 | `EnableMovieDbEpisodeGroupExtendedInfo` |
| 搜索偏好设置"屏蔽搜索推荐" | .31 | `BlockSearchRecommend` |
| 自动标记前集已播放（+阈值+排除收藏） | .30/.31 | `EnableAutoMarkWatched` + `AutoMarkWatchedThreshold` + `AutoMarkWatchedExcludeFavorites` |
| ClearTagline | .30 | `Mod/ClearTagline.cs` |
| PreferTraditionalChinese | .30 | `Mod/PreferTraditionalChinese.cs` |
| 美化多版本"按画质排序" | .32 | `EnableMultiVersionEnhance` + `MultiVersionSortOption` |
| 原生片头提取关键字黑名单 | .32 | `FingerprintBlacklistKeywords` |
| mdblist api 提升烂番茄评分 | .37 | `MdbListApiKey` + `DoubanAssist.UpdateMdbListRatingsAsync` |
| 解除同目录合并电影 8 部上限 | .37 | `FolderVideoGroupingLimit` + `FolderGroupingLimitTranspiler` |
| 追更模式最小等待时间 | .39 | `CatchupMinWaitMinutes` |
| 分布式/自定义 ffprobe（smb） | .39 | `CustomProbePath`/`CustomEncoderPath` + `FfmpegPathOverride` |
| 原语言海报区分简/繁中 | .42 | `PreferOriginalPoster` + `GetImageLanguagesParamPostfix` |
| 主屏显示媒体库项目数量 | .43 | `ShowLibraryItemCount` |
| 强制海报为竖图 / 集图像 16:9 | .43/.42 | `ForcePortraitPoster` / `EnsureEpisode16x9` |
| 片头片尾刮削器（含电影、优先级、apikey） | .44/.45 | `ScrapeIntroDbTask` + `UnifiedIntroDbProviders` + `IntroDbApiKey`/`TidbApiKey` |
| 隐藏设置 `ImageExtractionTimeoutMultiplier` | .51 | `ImageExtractionTimeoutMultiplier` |
| 移除 ts/m2ts 默认排除 | .50 | 默认 `ImageCaptureExcludeMediaContainers` 含 ts（对齐） |
| 仪表盘：内存占用 + 超限倒计时 + 联动重启 | .46/.49 | `ModOptions.RaceStatusList` + `EnableMemoryAutoRestart` + `MemoryAutoRestartThresholdMB` |
| HDR Vivid 检测与多版本美化 | .53 | `MultiVersionService.cs:201-203` 输出 "HDR Vivid" |
| 自动刮削片头片尾开关 | .53 | `EnableIntroDbScrape` + `Plugin.OnRefreshCompleted` 自动触发（**形态与 PRO 不同，见 2.3**） |
| 中文搜索增强（分词器/拼音库/特殊符号） | .26/.40/.55 | `Mod/EnhanceChineseSearch.cs` + libsimple 0.7.1 |
| **⬛ 领先 PRO**：`EnforceLibraryOrder`、`AutoMarkWatchedExcludeFavorites`、运维状态区（`RuntimeStatusList`/`EnableMemoryCleanup`/`CleanMemoryNow`）、`DirectLinkOptions`、`Core/` 版本适配层 | — | 勿被 PRO 反向覆盖 |

### 2.2 真实缺口清单（v4 新增/修订，按优先级）

#### P1 · 收尾小项（低风险，可立即做）

| 项 | 状态 | 说明 / 本地落点 |
|---|---|---|
| `ExclusiveControl` 枚举缺 3 项 | ❌ | 本地 [MediaInfoExtractOptions.cs:95-115](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MediaInfoExtractOptions.cs#L95-L115) 共 9 项；PRO 另含 `BlockRemoteFetcher`、`CatchAllAllowChapterImage`、`UnconditionalBlock` |
| `ImageCapturePostProcess.GenerateBackdrop` | ❌ | PRO 截图后处理三选：`EnsureAspect16By9`/`EnsureAspect2By3`/`GenerateBackdrop`；本地有前两者的等价开关（`EnsureEpisode16x9`/`ForcePortraitPoster`），**缺"生成 backdrop/fanart"** |
| `MetadataRefreshOptions.EpisodeRefreshLibraryScope` | ❌ | 剧集刷新按库范围（本地 `EpisodeRefreshScope` 只有刷新规则，无库范围） |
| `MetadataRefreshOptions.DoubanAccessToken` | ❌ | 豆瓣访问令牌（本地只有 `DoubanCookie`） |
| `RatingUpdateLookbackDays` | ✅ | 已实现（`MetadataEnhanceOptions.cs:124-128`）—— v3 的 ❌ 判定应改 ✅ |
| `ClearEmbeddedMetadata` | ❌ | 维持不做（PRO 混淆代码只见恢复管线旁路引用，语义不可确认）。changelog .50 印证其为"隐藏开关" |

#### P2 · UI / 前端注入

| 项 | 状态 | 说明 |
|---|---|---|
| `OptimizeMediaItemOrder` **四选** | 🔧 | 本地只落地 **NaturalSort**（`PinyinSortName` 数字补零）；PRO `ItemOrderOption` 另含 `ReverseSeasonOrder`（倒序季）、`ReverseEpisodeOrder`（倒序集）、`CollectionPerItemDateCreated`（按创建日期） |
| `ShowCollectionsFromSeasons` | ❌ | "某季在合集 → 整部剧视为合集成员"。语义已探明（PRO `A----------.cs:32280`），**挂点未定**（需找 Emby 4.10 合集成员枚举方法签名） |
| `EnforceUserPreferences`（强制季集显示样式） | ❌ | PRO `EnforceUserDisplayOption`：`LibraryOrder` / `SeriesDisplay`。本地 `EnforceLibraryOrder` 语义不同，**不可合并** |
| `LibraryScopePlayProgress`（跨库进度） | ❌ | 见 P4-5，UI 开关 + 数据面补丁 |
| JS 注入套件拆分 | 🔧 | 本地为单入口 `GetStrmAssistantJs` + `strmassistant.js`（663 行，已含 douban/versions/ui 模块）；PRO 拆为 `GetIndicatorJs`/`GetItemJs`/`GetListJs`/`GetLinkedItemsJs`/`GetListControllerJs`/`GetFilterMenuHtml`/`GetLibraryOptionsEditorJs`/`GetRefreshDialogJs`/`GetMediaInfoJs` 等。**形态差异非功能差异**，按需拆 |
| `SearchTuningPreferences`（搜索调优偏好组） | 🔧 | PRO 六项：`ExcludeOriginalTitle`✅、`SuppressSearchSuggestions`✅(=`BlockSearchRecommend`)、`TraditionalToSimplifiedChinese`✅(归一化内置)、**`DigitsAsTmdbId`❌**、**`MixedChineseVariants`🔧**、**`ShowMediaInfo`❌**（搜索结果页显示媒体信息，.46） |

#### P3 · 数据源与版本适配收尾

| 项 | 状态 | 说明 |
|---|---|---|
| **MovieDb 版本分档** | ❌ | 本地 [MetadataEnhanceOptions.cs:253-254](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MetadataEnhanceOptions.cs#L253-L254) 仅查程序集名；`MovieDbPluginVersion`（:259-275）仅诊断用，**未参与 gating**。PRO 依次适配 1.8.6/.7/.8、1.9.1.0、1.9.2.0、**1.9.9.0** |
| **HDR Vivid 前后端一致** | 🔧 | 后端已输出 "HDR Vivid"，但前端评分数组未同步（见 BUG-2） |
| 仪表盘搜索索引重建 | 🔧 | 本地有 `ScheduleIndexRebuildButton`（[GeneralOptions.cs:52-57](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/GeneralOptions.cs#L52-L57)），但 PRO 有独立 Dashboard 页 + 索引**优化/回收**（`Dashboard_Search_Index_Optimize_Confirm/Reclaimed`） |
| 仪表盘页（活动任务/会话） | ❌ | PRO Dashboard 含 `Active_Sessions`/`Active_Sessions_Playing`/`Pending_Refresh_Requests`/`Running_Catchup_Tasks`/`Running_Scheduled_Tasks`；本地为 `GeneralOptions.RuntimeStatusList` 状态区（形态近似、覆盖面较窄） |
| 演员刷新偏好多选化 | 🔧 | PRO `RefreshPersonOption` 五值（Default/Full×Metadata/Image）+ `PersonRefreshOption`（`DownloadImagesInAdvance`/`ExcludeIfChinese`）；本地 `RefreshPersonOption` 仅 3 值单一形态，**缺"预先下载头像"与"排除已有中文元数据"** |
| 豆瓣辅助刮削形态 | 🔧 | PRO `DoubanAssistScrapeOption` 四选（Cast/NameOverview/Poster/Rating）；本地用 4 个独立 bool 等价覆盖，**缺 `DoubanAccessToken`**、缺"批量应用豆瓣元数据按钮"（`DoubanScrapeButton`） |
| `LocalDiscoveryUrl` | ❌ | 自定义本地发现地址（含 `BLOCKED` 关键字，.28）。`NetworkOptions` 无此字段 |
| 日语元数据不经过简繁转换 | 🔧 | changelog .39；需核对 `PreferTraditionalChinese`/`LanguageUtility` 是否有语言白名单 |
| 冲突插件检测 | ❌ | PRO `PluginOptions_HasConflictPluginLoaded`（PRO .26 起"不再需要占位 dll，自动解决插件冲突"）。本地无冲突检测 |

#### P4 · 重型能力

见第一节（本地 TMDB 源 / ISO / 音乐持久化 / 跨库进度 / 分布式补 API）。

---

## 三、本地已做但需回归验证的项（v3 判定修正）

| 项 | v3 判定 | v4 修正 | 依据 |
|---|---|---|---|
| `RatingUpdateLookbackDays` | ❌ | ✅ 已实现 | `MetadataEnhanceOptions.cs:124-128` |
| `EnableAutomaticIntroScraping` | ✅ 等价 | 🔧 **形态不同** | 本地 `EnableIntroDbScrape` 隐含"自动刮削"；PRO 是独立开关（`IntroSkipScrapeOptions_EnableAutomaticIntroScraping`，默认 OFF，提示"可能拖慢扫库"）。本地缺"关闭自动、仅用任务手动刮"的粒度 |
| 搜索词繁→简 | ✅ | ✅ | `EnhanceChineseSearch` 双侧归一化 |
| `HidePersonOption.NonChinese` | ✅ | ✅ | `UIFunctionOptions.cs:31` |

---

## 四、bug 与版本适配风险清单（本轮机读 + 人工复核）

### 4.1 新发现 bug

**BUG-1（高，适配版本问题）· `EmbyApiVersion` 枚举数值序写反 + 重复取值**
- 文件：[Core/EmbyApiVersion.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Core/EmbyApiVersion.cs)
- `V4_9_1_80 = 49180`、`V4_9_1_90 = 49190`、`V4_9_2 = 49200`、**`V4_10 = 41000`**、`V4_10_0 = 41000`（**重复值**）。
- `DetermineApiVersion` 对 4.10.x 返回 `V4_10 = 41000`；而比较全部走 `_apiVersion >= minimumVersion`（[EmbyVersionAdapter.cs:108-111](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Core/EmbyVersionAdapter.cs#L108-L111)）。
- 后果：对运行 **4.10.1.0** 的实例，`IsVersionAtLeast(V4_9_1_80/90)`、`IsVersionAtLeast(V4_9_2)`、`IsVersionInRange(V4_9_2, V4_10)` **全部返回错误结果**（4.9.x 会被判为"更新"）。
- 当前未引爆：唯一消费点是 `MediaInfoApi.cs:67 IsVersionAtLeast(V4_9_1)`（4910 足够小，恒真）。属**高危埋雷**。
- 修法：数值改为与 `Version` 同序（例：`V4_9_1=4910, V4_9_1_80=49180, V4_9_1_90=49190, V4_9_2=49200, V4_10=41000`→ 应统一为 `V4_10 = 41000` 但 4.9.x 需改为 `409xx`，或干脆**改用 `Version` 比较、枚举只作标签**）；并消除 `V4_10_0` 重复。

**BUG-2（中）· HDR Vivid 前后端评分不一致**
- 后端：[MultiVersionService.cs:166](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Service/MultiVersionService.cs#L166) 给 "HDR Vivid" 打 **350** 分（HDR10+ = 400）；[:201-203](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Service/MultiVersionService.cs#L201-L203) 输出标签 "HDR Vivid"。
- 前端：[strmassistant.js:342](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Resources/strmassistant.js#L342) `const hdrs = ['DV', 'HDR10+', 'HDR'];` —— **未含 'HDR Vivid'**。
- 后果：`scoreOf` 里 `hdrs.indexOf('HDR Vivid') = -1 → Math.max(0,-1) = 0`，**Vivid 被当成 DV（最高档）排序**，与后端意图相反。
- 修法：前端数组补 `'HDR Vivid'`（并确保 `label` 未知时不误判最高档——建议 `indexOf` 为 -1 时取"最低档"而非 0）。

**BUG-3（中，适配版本问题）· MovieDb 无版本分档，1.9.9.0 未适配**
- [Mod/ChineseMovieDb.cs:44-46](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Mod/ChineseMovieDb.cs#L44) 硬编码 `ImportData` **5 参数** + 一串按方法名查找的反射；`IsMovieDbPluginLoaded` 只查程序集名。
- 用户当前 MovieDb = **1.9.5.0**（PRO 支持范围）；PRO .55 已适配 **1.9.9.0**。若用户升级 MovieDb，本地补丁可能**静默失效**（找不到方法 → 功能降级，不报错）。
- 修法：`IsMovieDbPluginLoaded`/`MovieDbPluginVersion` 接入分档；`OnInitialize` 对缺失方法统一 Warn（已有 `missingComponents` 范式，应扩展到 MovieDb）。

**BUG-4（低，已修待验证）· `FolderGroupingLimitTranspiler` 单形态匹配**
- [MergeMultiVersion.cs:223-271](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Mod/MergeMultiVersion.cs#L223-L271) 已扩展为 `Ldc_I4_8`/`Ldc_I4_S 8`/`Ldc_I4 8` 三形态 + `replaced==0` Warn。
- 残留风险：JIT 时机（首次渲染多版本页才触发）导致日志晚出；**建议在 4.9.1.90 与 4.10.x 双端各跑一次实测 IL**。

**BUG-5（低）· `GetStrmMountPath` 失败回退原路径**
- [LibraryApi.cs:1019-1024](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L1019-L1024)：挂载成功但取不到挂载路径时**返回原始 strm 路径**。消费方 `OptimizeSubtitle`（OpenSubtitles hash 搜索）会拿 strm 路径当本地文件，可能匹配失败但无明确日志。

**BUG-6（低，技术债）· `EmbyVersionCompatibility` 兼容层冗余且脆弱**
- [Common/EmbyVersionCompatibility.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/EmbyVersionCompatibility.cs) 自述"已被 EmbyVersionAdapter 取代"，但多个静态方法直接 `Plugin.Instance.Logger` / `EmbyVersionAdapter.Instance`（后者未初始化时抛异常）。保留 `Version49180/Version49190` 常量，**目前无消费点**（已 grep 确认）。

### 4.2 环境级 / 版本适配注意

- **目标 vs 实运行**：csproj 写 `mediabrowser.server.core 4.9.1.90`，但用户实跑 **4.10.1.0**（`D:\下载\embyserver-win-x64-4.10.1.0`）。→ 4.10 的 API 差异全靠 `EmbyVersionAdapter` 反射兜底，**未在 csproj 显式验证**；建议评估是否升 `mediabrowser.server.core` 引用版本或维持并补 4.10 冒烟清单。
- **MovieDb 版本**：本地实测 1.9.5.0；PRO 已到 1.9.9.0 → 见 BUG-3。
- **sqlite3.dll**：Emby 4.9.1.1+ 需替换为自编译版，否则中文搜索**静默失效**（环境级，非代码）。
- **libsimple 分词器**：x64 = 0.7.1、**arm64 锁 0.5.0**（上游 0.7.1 需 glibc ≥ 2.38）——**有意决策，非欠账**。
- **JS 选择器脆弱性**：`strmassistant.js` 依赖 `.card[data-id]`/`.mediaSources`/`.mediaSource`/`.mediaInfoItems`/`.itemMiscInfo` 等 Emby Web 内部类名，跨 Emby 版本/web 构建易失效（无降级保护）。

---

## 五、优化后的阶段计划（v4）

> 排序原则：**先清雷（bug）→ 再补低风险配置项 → 再补 UI/数据源 → 重型能力各自立项**。每阶段独立编译、独立验收。

### Phase 0 · 清雷与一致性（最高优先，工作量小）
1. **BUG-1**：修正 `EmbyApiVersion` 数值序 + 消除重复值；补单测式日志打印 `apiVersion`。
2. **BUG-2**：前端 `hdrs` 补 `'HDR Vivid'`，并修正未知 HDR/label 的兜底分级。
3. **BUG-3**：`MovieDbPluginVersion` 接入分档 + MovieDb 方法缺失告警。
4. **BUG-6**：删除或标注 `EmbyVersionCompatibility` 冗余常量（确认零引用后）。
5. **BUG-4**：4.9.1.90 / 4.10.x 双端 Transpiler 实测。

### Phase 1 · 收尾小项（P1，低风险）
- `ExclusiveControl` 补 3 项（`BlockRemoteFetcher` / `CatchAllAllowChapterImage` / `UnconditionalBlock`）。
- `ImageCapturePostProcess.GenerateBackdrop`（生成 backdrop）。
- `EpisodeRefreshLibraryScope`（剧集刷新按库范围）。
- `DoubanAccessToken`。
- 三语 resx + Designer 同步；ILRepack 参数核对。

### Phase 2 · UI / 前端（P2）
- `OptimizeMediaItemOrder` 四选补完（`ReverseSeasonOrder` / `ReverseEpisodeOrder` / `CollectionPerItemDateCreated`）。
- `ShowCollectionsFromSeasons`（**先探 Emby 4.10 合集成员枚举挂点**）。
- `EnforceUserPreferences` + `EnforceUserDisplayOption`（LibraryOrder/SeriesDisplay，**独立开关**）。
- `SearchTuningPreferences` 补 `DigitsAsTmdbId` / `ShowMediaInfo`，`MixedChineseVariants` 核对。
- JS 套件按需拆分（判据：单入口体积/可维护性，非功能缺口）。

### Phase 3 · 数据源与版本适配（P3）
- MovieDb 版本分档对齐 PRO（≤1.9.6 / 1.9.9.0）——**与 Phase 0 BUG-3 合并**。
- 仪表盘页（活动任务/会话/索引优化回收）与 `ScheduleIndexRebuildButton` 对齐。
- 演员刷新偏好多选化（`DownloadImagesInAdvance` / `ExcludeIfChinese` 等）。
- `LocalDiscoveryUrl`（含 `BLOCKED`）——先探 Emby 4.9/4.10 内部多点发现结构。
- 冲突插件检测（`HasConflictPluginLoaded`）。
- 日语元数据简繁白名单核对。

### Phase 4 · 重型能力（每项独立立项，按"价值/可行度"排序）
1. **本地 TMDB 缓存源**（价值最高）——先做**只读缓存**（限流/断网可用），再谈失效策略与"合并补丁"。
2. **分布式提取**——路线决策：① 维持 rffmpeg（插件侧已通）② 补 `SyncMediaInfo` API（集中共享）。**先决策再动手**。
3. **音乐持久化**——`PersistMediaInfoTask` 扩音频分支 + Album/Artist DTO。
4. **ISO/BDMV 提取**——**须先移除 [LibraryApi.cs:62/84](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/LibraryApi.cs#L62) 硬编码排除**，再补自建 runner + 分段探测 + `.iso.strm`。
5. **跨库多版本进度**——**放最后**（数据面高风险，需改 `PresentationUniqueKey` 链路）。

---

## 六、工程红线（沿用，均未失效）

1. **资源名下划线坑**：MSBuild 把嵌入资源路径 `-` 转 `_`（`linux_arm64`），须核对 `GetManifestResourceStream` 查找名。
2. **ILRepack 参数同步**：[StrmAssistant.csproj](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/StrmAssistant.csproj) 硬编码合并 `0Harmony`/`ChineseConverter`/`TinyPinyin`，新增依赖必须同步。
3. **resx 三语同步**：`Resources.resx`/`.zh.resx`/`.zh-hant.resx` + `Resources.Designer.cs`。
4. **技术栈约束**：net6.0 下统一 `IHttpClient` + `IJsonSerializer`，不引 `HttpClient`/`System.Text.Json`；**不引 PRO 的 `BlurayMounter` 等强依赖**。
5. **写回冲突**：豆瓣/本地 TMDB 一律"仅补空 + 字段级合并"。
6. **arm64 libsimple 锁 0.5.0**：有意决策。
7. **勿动清单（本地领先 PRO）**：`EnforceLibraryOrder`、`AutoMarkWatchedExcludeFavorites`、运维状态区、`DirectLinkOptions`、`Core/` 版本适配层。

---

## 七、验收基线（每 Phase 通用）

1. `dotnet build` 无错误；ILRepack 产物自动部署到实运行实例 `plugins` 目录。
2. x64 与 arm64 双端启动日志无 `Harmony not implemented` / `GLIBC not found`；`RuntimeStatusList` 各模块状态正常。
3. 新增配置项在 Emby 后台可见可改、重启后持久化，三语资源无缺。
4. 逐项回归：改配置 → 重启 → 验证行为 → 回滚配置 → 验证恢复。
5. **涉及 patch/Transpiler 的改动**，须在 4.9.1.90 与 4.10.x 两个 Emby 版本上各跑一次。
6. **Phase 0 专项**：启动日志出现 `API Version: V4_10`（而非 V4_9_x），且 `IsVersionAtLeast` 语义正确。

---

## 八、执行状态（滚动更新）

### ✅ 已完成 · Phase 0 清雷（提交 `90ff7ea`，已构建 + 已部署到 4.10.1.0 实例）
| 项 | 改动 | 文件 |
|---|---|---|
| BUG-1 | `EmbyApiVersion` 数值序改为 `major*1e6+minor*1e5+build*1e3+rev`；4.10=5000000 恒大于 4.9.x；删除重复的 `V4_10_0` | `Core/EmbyApiVersion.cs` |
| BUG-2 | `hdrs` 补 `'HDR Vivid'`；新增 `rankOf()`，未知档归最低档（修 `Math.max(0,-1)` 误判最高档） | `Web/Resources/strmassistant.js` |
| BUG-3 | 新增 `MovieDbVersion`/`MovieDbVersionTier`/`IsMovieDbVersionCompatible`；`ChineseMovieDb.OnInitialize` 输出缺失组件告警 | `Options/MetadataEnhanceOptions.cs`、`Mod/ChineseMovieDb.cs` |
| BUG-6 | 删除零引用常量 `Version4910/80/90` | `Common/EmbyVersionCompatibility.cs` |
| **新增 bug（构建时发现）** | 修复 `Resources.resx` / `Resources.zh.resx` 的**跨语言重复键**（MSB3568 静默丢译文：英文 resx 混入中文块、简体 resx 混入繁体块） | `Properties/Resources*.resx` |

### ✅ 已完成 · Phase 1（部分，提交 `90ff7ea`）
| 项 | 结论 |
|---|---|
| `EpisodeRefreshLibraryScope`（剧集刷新按库范围） | ✅ 新增配置项 + `LibraryApi.IsItemLibraryInScope` 过滤 + 三语资源 + Designer |
| resx 重复键 | ✅ 已清理（9 警告 → 1 警告，剩余为既有 CS1998） |

### ⏸ 已评估但**暂缓**（附理由，非遗漏）
| 项 | 暂缓理由 |
|---|---|
| `ExclusiveControl` 补 3 项（BlockRemoteFetcher / CatchAllAllowChapterImage / UnconditionalBlock） | 仅知选项名、不知 PRO 精确语义；无条件加枚举 = UI 可见但无行为的死开关，违反"宁缺毋滥"红线。需先实探语义再落地 |
| `ImageCapturePostProcess.GenerateBackdrop` | 需新增"从视频生成 backdrop 图并写回 Backdrop 类型"链路，非配置项改动；语义（何时生成/覆盖策略）未确认，暂缓 |
| `DoubanAccessToken` | 本地豆瓣走 `movie.douban.com` HTML + `subject_suggest` + wikidata + mdblist，**未使用 frodo v22 API**，该令牌本地无落点；若将来接入 frodo 再加 |

### ⏭ 未开始
- Phase 2（UI：OptimizeMediaItemOrder 四选补完 / ShowCollectionsFromSeasons / EnforceUserPreferences）
- Phase 3（MovieDb 1.9.9.0 实测适配 / 仪表盘 / 刷新偏好多选 / LocalDiscoveryUrl / 冲突检测）
- Phase 4（本地 TMDB 源 / 分布式 API / 音乐持久化 / ISO / 跨库进度）

### 构建/部署记录
- `dotnet build -c Release`：**0 错误**；警告 9 → 1（仅剩既有 `SubtitleApi.cs:205 CS1998`）。
- 部署：`D:\下载\embyserver-win-x64-4.10.1.0\programdata\plugins\StrmAssistant.dll`（13:26，7,117,824 字节）。
- **待用户重启 Emby 验证**：启动日志应出现 `API Version: V4_10`；`ChineseMovieDb: MovieDb <ver> (tier ...) - all components located` 或明确的缺失告警。
