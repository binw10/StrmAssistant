> 本地工程：`D:\下载\AI\StrmAssistant-myp015`（分支 myp015，HEAD `325a916` + 未提交在制品；插件版本 26.3.25 / net6.0）
> 参照对象：PRO **v3.0.0.54**（Wiki「PRO版的功能」功能清单 + `_pro_tmp/decompiled` 反编译逐字段核对）
> 本版为 v2，替代并修正早前的 `docs/PRO功能移植方案.md`；日期 2026-10-15

---

## 〇、先厘清三个项目（原表述中路径有交叉）

| 代号 | 实体 | 关键事实 |
|---|---|---|
| **社区版** | `sjtuross/StrmAssistant` 免费开源版 | Wiki 明确：仅支持 Emby **4.8.5.0–4.8.11.0**、MovieDb ≤1.8.6，**不再适配其它版本**；功能更新全部转入 PRO |
| **本地分支** | `wabisabi926/StrmAssistant` 分支 `myp015`（即当前工作区） | 社区版底座 + AI 适配最新 Emby（4.9.1.90 / 4.10.0.29），并叠加豆瓣辅助、IntroDb、影音直链、运维状态区等自有功能 |
| **PRO** | `sjtuross/StrmAssistant` 付费版 **v3.0.0.54** | 二进制与反编译源码位于 `D:\下载\AI\StrmAssistant.Releases\_pro_tmp\`（`StrmAssistantPro.dll` + `decompiled/`） |

> 结论：`StrmAssistant-myp015` 是**社区版分支**，不是 PRO；PRO 的实体代码在同级 `StrmAssistant.Releases`。Wiki 指 `sjtuross/StrmAssistant.wiki`（PRO 口语文档）。

---

## 一、核对方法

1. 从 PRO 反编译的 `StrmAssistant.Options*` 全部目录提取公开属性名（共 214 个配置字段）。
2. 与本地 `StrmAssistant/Options/**/*.cs` + `Properties/*.resx` 做存在性比对。
3. 对**命名不同但语义等价**的字段做人工映射（下表已标注「等价」）。
4. 结论以源码为准，不采信旧文档的间接推断。

### 核对结论（要点）

1. **旧文档有 3 处结论是错的**：媒体信息/视频截图黑名单、提取超时、`ClearEmbeddedMetadata` 中的前两组**本地已实现**（[MediaInfoExtractOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/MediaInfoExtractOptions.cs#L51-L78)），不是缺口。
2. 本地已吸收社区版全部功能，且旧移植计划（UI 增强 / 豆瓣 / IntroDb / 多版本画质）**已全部落地**。
3. 与 PRO v3.0.0.54 逐字段核对后，**真实缺口 30 项**：其中「既有模块能力落后」11 项、「完全缺失的新能力」19 项。
4. 本地**独有且领先 PRO** 的 4 项，勿被反向覆盖：`EnforceLibraryOrder`、`AutoMarkWatchedExcludeFavorites`、`EnableMemoryAutoRestart`/`EnableMemoryCleanup` 运维状态区、`DirectLinkOptions` 影音直链。
5. **在制品（未提交）**：#34 `FolderVideoGroupingLimit`、#12 `CustomProbePath`/`CustomEncoderPath` + 新增 [FfmpegPathOverride.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Common/FfmpegPathOverride.cs) —— 属 Phase 1，收口即可提交。

---

## 二、功能对账矩阵（按 Wiki 分类，✅已有 ⚠️部分/形态不同 ❌缺 🔧在制）

### 搜索
| PRO 功能 | 状态 | 本地实现 / 差距 |
|---|---|---|
| 重构中文搜索增强、自适应分词器 | ⚠️ | 已自研 [EnhanceChineseSearch.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Mod/EnhanceChineseSearch.cs)；但分词器非自适应（x64 libsimple 0.7.1 / arm64 0.5.0 固定），PRO 跟随数据库当前分词器自适应 |
| 完全适配 4.9 数据库连接池 | ⚠️ | 已用 SQLitePCLRawEx，缺 4.9 连接池专项 patch |
| 搜索词转换（繁→简） | ❌ | `CHTCHSConv` 已随 ILRepack 打进产物，可复用 |
| 简繁混搜 | ⚠️ | 依赖 libsimple 0.7.1（仅 x64） |
| 屏蔽搜索推荐列表 | ❌ | 需前端 JS |
| `SearchTuningPreferences` / `EnhanceChineseSearchApply` | ❌ | PRO ModOptions 字段，本地无 |

### 网络
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| 代理「刮削白名单 / 全局代理」双模式 | ❌ | 本地仅 [NetworkOptions.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Options/NetworkOptions.cs#L21-L35) 单开关 `EnableProxyServer` |
| `LocalDiscoveryUrl` 自定义本地发现地址 | ❌ | 无 |

### 媒体信息
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| 关键字/容器黑名单（媒体信息、截图、BIF） | ✅ | 本地已有 4 组黑名单字段，早前文档误判为缺 |
| `MediaInfoExtractionTimeoutMs` | ✅ | 已有 |
| `ImageExtractionTimeoutMultiplier` | ❌ | 新增：截图超时倍数 |
| `ClearEmbeddedMetadata` 清内嵌元数据 | ❌ | 待补 |
| `ImageCapturePostProcessMode` 截图后处理模式化 | ⚠️ | 本地用 `ForcePortraitPoster`/`EnsureEpisode16x9` 两个开关，功能等价、形态不同 |
| 自定义 ffmpeg/ffprobe | 🔧 | 在制（`CustomProbePath`/`CustomEncoderPath` + `FfmpegPathOverride`） |
| ISO 媒体信息提取（多线程） | ❌ | 需静态 ffprobe + 分段探测 |
| 音乐持久化 json（专辑/艺术家/内嵌封面） | ❌ | [PersistMediaInfoTask.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/ScheduledTask/PersistMediaInfoTask.cs) 仅视频 |
| 分布式媒体信息提取 | ❌ | = `POST /emby/Items/SyncMediaInfo` 壳 + rffmpeg 运维，插件侧 ≈2–3 天 |

### 元数据
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| 豆瓣辅助刮削 | ✅ | `EnableDoubanAssist` 等 8 开关 + `GetDoubanRatings` API |
| 豆瓣磁盘缓存 + 构建/触发任务 | ❌ | 本地为即时 API，PRO 可离线批量（`DoubanScrapeButton`/`DoubanScrapeProgress`） |
| `DoubanAssistScrapeScope` 按库生效 | ❌ | 本地为全局开关 |
| 评分源扩展（`MdbListApiKey`/`RatingUpdateLookbackDays`/`DoubanClient`/`DoubanAccessToken`） | ❌ | 本地仅有 `DoubanCookie` |
| 本地 TMDB 刮削源 | ❌ | PRO `MetadataCacheMode`/`MetadataCacheRootFolder`/`MetadataCacheOptionalImageTypes`/`ClearRecentTmdbCacheOnStartup`，本地全无 |
| 简中/繁中海报与徽标区分 | ❌ | 需图像语言路由 |
| 新建媒体库默认 TMDB | ❌ | 需 patch `VirtualFolder` 创建 |
| `PreferTraditionalChinese` 备选语言优先繁中 | ❌ | 依赖简繁转换基建 |
| `ClearTagline` 清标语 | ❌ | 小项 |
| `EnableMovieDbEpisodeGroupExtendedInfo` | ❌ | 本地有 `MovieDbEpisodeGroup`，缺 json 扩展自定义季简介/首播 |
| 演员刷新偏好组合 | ⚠️ | 本地 `RefreshPersonMode` 单值，PRO `PersonRefreshPreferences`/`PersonRefreshOption` 多选 |
| 元数据刷新偏好 | ⚠️ | 本地 `EpisodeRefreshScope`/`EpisodeRefreshLookbackDays`，PRO 为 `MetadataRefreshPreferences`/`MetadataRefreshLookbackDays` |

### 片头
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| 分库自定义声纹探测时长 | ❌ | 本地仅全局 `IntroDetectionFingerprintMinutes`，PRO 有 `IntroSkipFingerprintOptions` 按库 |
| `ParallelMarkerComputation` 并行声纹 | ❌ | 最大化利用 CPU |
| Unified IntroDb 完整版 | ⚠️ | 本地已接 theintrodb（`EnableIntroDbScrape`+`IntroDbApiKey`），缺 TIDB 第二源 `TidbApiKey`/`UnifiedIntroDbProviders` 与 `EnableAutomaticIntroScraping` |
| `IntroMarkerProtector` 防覆盖 | ❌ | 需 Harmony patch 保护在线标记 |
| 黑名单关键字 | ✅ | 等价：本地 `FingerprintBlacklistKeywords`/`FingerprintBlacklistShows` |

### 字幕
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| `OptimizeSubtitle` 为 Strm 优化 OpenSubtitles | ❌ | 需 HEAD 预检 + `Tier2MaxConcurrentCount` 节流（本地已有该字段但未接此链路） |
| 外挂字幕扫描支持外挂音轨 | ⚠️ | 本地 `ScanExternalSubtitleTask` 仅字幕 |

### 多版本
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| `LibraryScopePlayProgress` 跨库独立进度/收藏 | ❌ | 高价值 |
| `FolderVideoGroupingLimit` 解 8 部上限 | 🔧 | 在制 |
| 多版本美化/画质排序 | ⚠️ | 本地 `EnableMultiVersionEnhance` + `MultiVersionService` 已实现；PRO 另有 `MultiVersionDisplayOption`/`SortOption`/`ReorderPreference` 细分字段组 |

### 通知
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| 合集项目变更通知 | ⚠️ | 有 `favorites.update`，缺 Boxset 专属类型 |
| 媒体元数据更新通知 + `MetadataUpdateTrackedFields` | ❌ | 高价值 |
| 媒体图像更新通知 | ❌ | 小项 |

### 体验增强
| PRO 功能 | 状态 | 差距 |
|---|---|---|
| 自动标记前集已看 | ✅ | 本地 `EnableAutoMarkWatched` 且多「排除收藏」，**超越 PRO** |
| 强制库顺序 | ✅ | 本地 `EnforceLibraryOrder`（等价 PRO `EnforceUserPreferences`），**领先** |
| 隐藏无图演员 | ⚠️ | 本地两选（NoImage/ActorOnly），PRO 三选（多 `NonChinese`） |
| `ReplaceUnplayedWithTotal` 显示总集数 | ❌ | 需 JS + patch |
| `OptimizeMediaItemOrder` + `OptimizeItemOrderPreferences` | ❌ | PRO 有四选偏好组，本地无 |
| `ShowCollectionsFromSeasons` 季的合集归属 | ❌ | 无 |
| `ShowLibraryItemCount` 主屏角标 | ❌ | 需 JS |
| `ReverseLogLines` 日志从新到旧 | ❌ | 小项 |
| `SuppressPluginUpdates` 屏蔽插件自动更新 | ❌ | 无 |
| 快捷菜单：删演员/清章节图与 BIF/清媒体信息 | ✅ | 已实现 |

### 前端注入套件
| PRO 功能 | 状态 |
|---|---|
| `GetIndicatorJs`/`GetItemJs`/`GetListJs`/`GetRefreshDialogJs`/`GetFilterMenuHtml` 等 | ❌ 本地仅有 [GetStrmAssistantJs.cs](file:///d:/下载/AI/StrmAssistant-myp015/StrmAssistant/Web/Api/GetStrmAssistantJs.cs) 单入口 |

---

## 三、实施阶段（低风险高回报 → 重架构）

### Phase 1 · 收口在制品 + 配置项补完（小改动，纯后端）
1. **🔧 先收口在制**：`FolderVideoGroupingLimit`（`MergeMultiVersion.UpdateFolderGroupingLimit`）、`CustomProbePath`/`CustomEncoderPath` + `FfmpegPathOverride`，跑通 x64/arm64 后提交。
2. **新增黑名单/超时补漏**：`ImageExtractionTimeoutMultiplier`、`ClearEmbeddedMetadata`。
3. **#分库声纹时长**：`IntroSkipOptions` 加按库字典（`IntroDetectionFingerprintMinutes` 按库查表）。
4. **#并行声纹** `ParallelMarkerComputation`：`Parallel.For` + 信号量限流。
5. **代理双模式 + `LocalDiscoveryUrl`**：`NetworkOptions` 加 `ProxyMode`(None/ScrapeOnly/Global) + 白名单 + 发现地址。
6. **小开关**：`ClearTagline`、`DoubanAssistScrapeScope`、`SuppressPluginUpdates`。
7. **通知补全**：`metadata.update` / `image.update` / `boxset.update` + `MetadataUpdateTrackedFields` 字段过滤。

### Phase 2 · UI / 前端注入补完
1. **JS 套件**：仿 `GetStrmAssistantJs` 隐藏路由范式，补 `GetIndicatorJs` / `GetItemJs` / `GetListJs`。
2. `ReplaceUnplayedWithTotal` + `ShowLibraryItemCount`（靠 JS 渲染）。
3. `OptimizeMediaItemOrder`（含 `OptimizeItemOrderPreferences`）、`ShowCollectionsFromSeasons`。
4. 屏蔽搜索推荐列表；`ReverseLogLines`。
5. 搜索词转换（繁→简）接到搜索入口（复用 `CHTCHSConv`）；`EnableChineseSearchApply`/`SearchTuningPreferences`。

### Phase 3 · 数据源与字幕扩展
1. 豆瓣磁盘缓存 + `BuildDoubanCacheTask` + 触发任务（并发安全 / 损坏恢复）。
2. Unified IntroDb 完整版：TIDB 第二源 + 聚合 + `EnableAutomaticIntroScraping`；配 `IntroMarkerProtector`。
3. `OptimizeSubtitle`：OpenSubtitles HEAD 预检 + 节流。
4. 外挂音轨扫描：扩展 `ScanExternalSubtitleTask`。
5. 增量小项：`EnableMovieDbEpisodeGroupExtendedInfo`、演员/元数据刷新偏好组合、评分源扩展（MdbList）、`HidePersonOption.NonChinese`。
6. 简繁基建收尾：`PreferTraditionalChinese`、简繁海报徽标区分、新建库默认 TMDB。

### Phase 4 · 重型能力（每项独立立项）
1. **本地 TMDB 刮削源**：`MetadataCacheMode/RootFolder/OptionalImageTypes` + 磁盘持久化（TMDB 限流/断网时仍可刮削）——**长期价值最高**。
2. **ISO 提取**：静态 ffprobe + 多线程分段探测（官方 ffprobe 不支持）。
3. **音乐持久化**：`PersistMediaInfoTask` 扩展专辑/艺术家/内嵌封面序列化与恢复。
4. **分布式提取**：`SyncMediaInfo` API 壳 + rffmpeg 负载均衡（复用 Phase 1 的 `CustomProbePath`/`CustomEncoderPath`，节点部署为纯运维）。
5. **跨库多版本进度** `LibraryScopePlayProgress`（需改播放进度键）。

---

## 四、工程红线

1. **资源名下划线坑**：MSBuild 会把嵌入资源路径 `-` 转 `_`，新增平台资源须用 `linux_arm64` 风格命名并核对 `GetManifestResourceStream` 查找名（参考 `55419ac`）。
2. **ILRepack 参数同步**：`csproj` 的 `PostBuildMerge` 硬编码合并 `0Harmony`/`ChineseConverter`/`TinyPinyin`，新增依赖库必须同步改参数，否则运行时缺 dll。
3. **resx 三语同步**：新增配置项须同步 `Resources.resx`/`.zh.resx`/`.zh-hant.resx` + `Resources.Designer.cs`。
4. **技术栈约束**：net6.0 下统一 `IHttpClient` + `IJsonSerializer`，不引 `HttpClient`/`System.Text.Json`。
5. **写回冲突**：豆瓣/本地 TMDB 一律「仅补空 + 字段级合并」；`IntroMarkerProtector` 必须防在线标记被本地声纹覆盖。
6. **arm64 libsimple 锁定 v0.5.0**：上游 0.7.1 要求 glibc ≥2.38，属有意决策，不是欠账。
7. **勿动清单**（本地领先 PRO）：`EnforceLibraryOrder`、`AutoMarkWatchedExcludeFavorites`、运维状态区（`EnableMemoryAutoRestart`/`EnableMemoryCleanup`）、`DirectLinkOptions`。

---

## 五、验收基线（每 Phase 通用）

1. `dotnet build` 无错误；ILRepack 产物自动部署到 `%APPDATA%\Emby-Server\programdata\plugins`。
2. x64 与 arm64 双端启动日志无 `Harmony not implemented` / `GLIBC not found`；启动总览里各模块状态正常。
3. 新增配置项在 Emby 后台可见可改、重启后持久化，三语资源无缺。
4. 逐项回归：改配置 → 重启 → 验证行为 → 回滚配置 → 验证恢复。
