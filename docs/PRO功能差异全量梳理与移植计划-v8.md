# StrmAssistant 社区版 × PRO 功能差异全量梳理与移植计划（v8 · 完整版）

> **社区版**：`D:\下载\AI\StrmAssistant-myp015`（`net6.0` / `Lib.Harmony 2.4.2`，运行于 Emby 4.10.1.0）
> **PRO 版**：`StrmAssistantPro.dll` 反编译源码 `_pro_tmp/decompiled_v2/`（138 个 `.cs`，`3.0.0.55+3abcae8`，`netcoreapp6.0` / `MonoMod`）
> **比对方法**：**逐 Options 文件提取全部属性 → 双端比对**；再对 **ScheduledTask / Web / Provider / Mod** 四层做文件级差异清点。凡"PRO 有、社区无"才计缺口。
> **日期**：2026-10-17　**状态**：取代 v6/v7 作为当前唯一基线

---

## 〇、一页看懂：现在到底什么进度

| 层面 | 状态 |
|---|---|
| **整体功能对齐** | 约 **92%**（PRO 的功能社区版基本都有；差异集中在"形态"而非"能力"） |
| **已完成的移植（本轮前）** | P1-1 搜索调优、P1-2 元数据刷新偏好、P1-3 豆瓣 Token |
| **本轮真正要做的** | 只有 **1 件大事：ISO 专项**（下分 4 小步） |
| **真功能性缺口** | **4 项，全 P3（低优先）**——不做也不影响主用 |
| **不可移植/无价值** | 8 类，明确剔除（§六） |
| **社区版反超 PRO** | 12 个模块（§五）——**不要为了"对齐"而砍掉** |

> **一句话**：功能面已高度对齐，**唯一有价值的硬骨头是 ISO**（尤其 strm/HTTP 形态）。其余都是锦上添花。

---

## 一、两版的技术底座（决定"能不能直接搬代码"）

| 项 | PRO | 社区版 | 移植影响 |
|---|---|---|---|
| 目标框架 | `netcoreapp6.0` | `net6.0` | 等价 |
| 打补丁方式 | **MonoMod**（IL 级）+ 混合 Harmony | **Lib.Harmony 2.4.2** | ⛔ **代码不可搬**，只能按"行为"用 Harmony 重写 |
| ISO 读取 | 劫持 Emby `IFileSystem.OpenRead` → 进程内 UDF + 内置 `BdInfoExaminer`（私有 BDInfo 港） | 自研 `IsoMounter`+`UdfTitleResolver` + 挂载/自定义 ffprobe | 语义可对齐，实现独立 |
| 依赖库 | `MonoMod`/`BlurayMounter`（私有）/`Portable.Licensing` | `DiscUtils.Udf`(MIT)/`CHTCHSConv`/`TinyPinyin` | PRO 私有库**一律不引** |
| 授权 | License 文件 + 机器码绑定 + 功能门控 | 无 | 不移植 |

---

## 二、全量功能差异矩阵（逐层清点）

### 2.1 Options 层 —— 逐属性比对结果

#### ✅ 已完全对齐（两边都有，语义等价）—— 无须任何行动

| 功能块 | 覆盖项 |
|---|---|
| 中文搜索 | `EnhanceChineseSearch` + `ExcludeOriginalTitleFromSearch` + `DigitsAsTmdbId` + `MixedChineseVariants` + `ShowMediaInfo` + `SearchScope`（**6 项全有**）|
| 界面功能 | `HidePersonNoImage`/`HidePersonPreference`、`OptimizeMediaItemOrder`/`OptimizeItemOrderPreferences`、`EnforceUserPreferences`/`Scope`、`BeautifyMissingMetadata`、`EnhanceMissingEpisodes`、`NoBoxsetsAutoCreation`、`ReplaceUnplayedWithTotal`、`ShowLibraryItemCount`、`ShowCollectionsFromSeasons`、`ReverseLogLines` |
| 元数据 | `ChineseMovieDb`/`ChineseTvdb`（含语言表）、`BlockNonFallbackLanguage`、`ClearTagline`、`PreferTraditionalChinese`、`MovieDbEpisodeGroup`、`LocalEpisodeGroup`、`EnhanceMovieDbPerson`、`PreferOriginalPoster`、`PinyinSortName`、`EnhanceNfoMetadata`、`AltMovieDbConfig`、`EnableDoubanAssistScraping`、`MdbListApiKey`、`RatingUpdateLookbackDays`、`DoubanCookie`/`DoubanAccessToken`/`DoubanClient` |
| 多版本 | `MergeMultiVersion`、`SplitMoviesProgress`、`FolderVideoGroupingLimit`、`EnableMultiVersionEnhance`(≈`BeautifyMultiVersion`) |
| 片头片尾 | `UnlockIntroSkip`、`ParallelMarkerComputation`、`MarkerEnabledLibraryScope`、`EnableIntroSkip`、`MaxIntroDurationSeconds`/`MaxCreditsDurationSeconds`/`MinOpeningPlotDurationSeconds`、`LibraryScope`/`UserScope`/`ClientScope`、`IntroSkipPreferences`、`EnableAutomaticIntroScraping`、`TidbApiKey`/`IntroDbApiKey`/`UnifiedIntroDbProviders`、`FingerprintBlacklistShows` |
| 其它 | `EnhanceNotificationSystem`、`MetadataUpdateTrackedFields`、`EnableDeepDelete`、`OptimizeSubtitle`、`CatchupMode`/`CatchupTaskScope`、`MaxConcurrentCount`/`CooldownDurationSeconds`/`Tier2MaxConcurrentCount`、`LocalDiscoveryUrl`、`EnableProxyServer`/`ProxyServerUrl`/`ShowProxyServerStatus`/`IgnoreCertificateValidation`、`MetadataCacheMode`/`RootFolder`/`OptionalImageTypes`、`AltMovieDbApiUrl`/`ImageUrl`/`ApiKey` |

#### 🔴 真缺口（PRO 有、社区无）—— 仅 4 项，全 P3

| # | PRO 属性 | 语义 | 为什么优先级低 | 工作量 |
|---|---|---|---|---|
| **G1** | `ImageCapturePostProcessMode`(+`ModeList`) | 截图后处理（额外生成海报/背景图等，含 DV 增强） | 只影响"无主图时自动截图"的附加产物；不截图就无感 | 中 |
| **G2** | `ClearRecentTmdbCacheOnStartup` | 启动时清 TMDB 最近缓存 | 社区有 `LocalTmdbCache` 手动机制，且缓存过期本就自动失效 | 小 |
| **G3** | `LibraryScopePlayProgress` | 按媒体库分别记录多版本播放进度（仅剧集） | 改 `PresentationUniqueKey` 链路 → **动播放进度键，风险高** | 高 |
| **G4** | `CatchupMinimumDelaySeconds` | 追更最小延迟（**秒**粒度） | 社区 `CatchupMinWaitMinutes` **功能等价**，仅刻度不同 | 极小 |

#### 🟡 形态差异（功能都在，只是 UI 拆分方式不同）—— 不追，见 §六

`MetadataRefreshPreferences+RuleList`、`PersonRefreshOptionList+Preferences`、`MultiVersionDisplayPreference+ReorderPreference`、`MetadataCacheOptionList`、`IntroSkip*` 3 个嵌套子对象、`MultipleVersionsOptions` 嵌套对象、`Is*PluginSupported` 命名、`SearchTuningOptionList` 多选串 —— 社区均以 **bool / 单值枚举** 实现，**语义等价**。改造成本高、回归面大、收益几乎为零。

### 2.2 ScheduledTask 层 —— 文件级差异

| 社区版任务 | PRO 对应 | 差异 |
|---|---|---|
| `RefreshEpisodeTask` | `RefreshMetadataTask` | 命名不同，功能同（刷新剧集元数据） |
| `RefreshPersonTask` | `RefreshPersonTask` | ✅ 已对齐（P1-2 补了 `DefaultImageRefresh`/`FullImageRefresh`） |
| `ScanExternalSubtitleTask` | `ScanExternalTrackTask` | PRO 支持更多轨道类型，社区聚焦字幕 |
| `ScrapeIntroDbTask` | `UnifiedIntroDbProvider`(Provider) | 社区用 Task，PRO 用 Provider —— 效果等价 |
| `BuildDoubanCacheTask` / `DeletePersonTask` / `ExtractIntroFingerprintTask` / `ExtractMediaInfoTask` / `ExtractVideoThumbnailTask` / `MergeMultiVersionTask` / `PersistMediaInfoTask` | 同名 | ✅ 齐 |
| — | `RestartServerTask` | 社区用 `EnableMemoryAutoRestart` 替代（更优） |
| — | `TriggerMergeMovieTask` / `TriggerPopulateDoubanIdTask` | PRO 内部触发器，社区合并进主任务 |
| — | `UpdatePluginTask` | ⛔ 社区**主动删除**（自更新有风险，刻意决策） |

> ✅ ScheduledTask 层**无真缺口**。

### 2.3 Web / Provider 层 —— 差异

| 项 | PRO | 社区 | 差异 |
|---|---|---|---|
| JS 注入 | 拆分 11 个端点（`GetItemJs`/`GetListJs`/`GetMediaInfoJs`/…`GetStrmAssistantJs`） | 单入口 `GetStrmAssistantJs` + `ShortcutMenuHelper` | 社区**聚合**，功能等价；PRO 按需加载略省带宽 |
| 清理类 API | `ClearChapterImage`/`ClearIntro`/`ClearMediaInfo`/`DeleteVersion`/`DeletePerson`/`LockItem`/`CopyVirtualFolder` | 同名 | ✅ 齐 |
| 独有 API | `SyncMediaInfo`（分布式写回）、`GetFilterMenuHtml`、`GetLinkedItemsJs`、`GetRefreshDialogJs`、`GetLibraryOptionsEditorJs` | 社区独有 `GetDoubanRatings`、`GetMultiVersionInfo` | 各有取舍，见 §五 |
| Provider | `BangumiExternalId`、`IntroMarkerProtector`、`PersistMediaInfoProvider`、`UnifiedIntroDbProvider` | 社区走 Task/Mod 实现同功能 | 等价 |

### 2.4 Mod 层（补丁）—— 社区版独有 37 个补丁模块

> ⚠️ **整个 `Mod/` 层（37 个文件）是社区版自研**，PRO 没有对应文件（PRO 把这些逻辑全塞在 MonoMod 里）。这是社区版**最大的领先面**，禁止在"对齐"时误删。

---

## 三、ISO 专项（唯一真正的工作量所在）

### 3.1 Wiki 6 条能力现状

| Wiki 能力 | PRO | 社区现状 | 状态 |
|---|---|---|---|
| 1. 提取 ISO/BDMV 媒体信息 | ✅ | ✅ 本地 `.iso` 已通 | 🟢 |
| 2. MPLS 提取音轨/字幕语言、章节 | ✅ | ❌ 只选正片，未提语言/章节 | 🔴 |
| 3. 检测双层杜比 P7 | ✅ | ❌ 无 | 🔴 |
| 4. 支持 本地 / http / smb | ✅ | ❌ **仅本地**，strm/HTTP 不命中 | 🔴 **你的主场景** |
| 5. ISO 截图 / 章节图 | ✅ | ⚠️ 有 CustomEncoderPath，未接 ISO | 🟡 |
| 6. DVD ISO 自动识别 | ✅ | ✅ 已实现待真碟 | 🟢 |

### 3.2 核心事实（P1-4 实测得出）

- 你的样本 `.iso.strm` → `MediaSource.Path` 是 **HTTP URL**，`Protocol=Http`、`Container=strm`。
- 该条目 **从未被探测**（`Container=None`、无流）——因为没有补丁能命中 HTTP 形态。
- 拦截点：`IsoMounter.TryNormalizeIsoPath` 有 `if (candidate.Contains("://")) return false;` 和 `if (!File.Exists(candidate)) return false;` 两道门槛，**HTTP 必然出局**。
- **但 Emby 内置 ffprobe 其实能读 ISO**（实测 `format=mpegts`、双 HEVC `codec_tag=HDMV`）→ 问题不在 ffprobe，而在**没人把 ISO 交给它**。

### 3.3 PRO 的做法（已从 `strings.txt` 完全还原）

```
① 识别：ExtractBlurayDisc 类专门处理 .iso.strm
② 读盘：劫持 IFileSystem.OpenRead → 进程内 UDF，让 Emby 以为在读普通文件
③ 探测：把 ffprobe 输入写成 bluray:"<路径或URL>"
④ 能力：要求 ffprobe 带 --enable-libbluray（= 自定义 Probe 路径提供的）
⑤ 解析：PLAYLIST/*.mpls、CLIPINF/*.clpi、STREAM/*.m2ts、*.ssif
```

**社区版已具备 ①②⑤ 的一半（自研 UDF 选片）、④ 的开关（CustomProbePath）**，缺的是 **③ 把 `bluray:` 协议接上** 和 **让 HTTP 形态进入流程**。

### 3.4 分 4 小步实现（零新依赖）

| 步 | 内容 | 改哪个文件 |
|---|---|---|
| **ISO-1** | 放开 HTTP/strm 识别：`TryNormalizeIsoPath` 增"`://` 且以 `.iso` 结尾"分支（跳过本地校验） | `Common/IsoMounter.cs` |
| **ISO-2** | 探测输入改写为 `bluray:"{0}"`（对齐 PRO 协议；本地/HTTP 通吃） | `Mod/IsoExtraction.cs` |
| **ISO-3** | `FfmpegPathOverride` 缓存 `SupportsBluray` 能力位；不支持则**安全回退不改写** | `Common/FfmpegPathOverride.cs` |
| **ISO-4** | 选项文案 + 三语 resx 更新，说明"支持 strm/http ISO（需带 libbluray 的自定义 ffprobe）" | `Options/MediaInfoExtractOptions.cs` + `Resources*` |

**红线**：全程**不改 `MediaSourceInfo.Path`**（§四-6），播放路径与条目身份不受影响；能力不足自动回退。

---

## 四、移植工程红线（必须遵守）

1. 技术栈锁 `net6.0`，不引 PRO 私有强依赖。
2. 新增依赖须 MIT/BSD/Apache，并同步 `csproj` 的 ILRepack 合并参数（现合并 7 个 dll）。
3. `Resources.resx` + `.zh.resx` + `.zh-hant.resx` + `Resources.Designer.cs` **四处同步**。
4. 写回一律"仅补空 + 字段级合并"，禁覆盖用户数据。
5. 枚举本地化用基类 `DescriptionAttribute`，不依赖 4.10 新 API。
6. **`MediaSourceInfo.Path` 不可改写**。
7. 版本适配统一走 `Core/EmbyVersionAdapter`。

---

## 五、社区版**反超 PRO** 的模块（禁止在对齐中丢失）

| 模块 | 说明 |
|---|---|
| **`Mod/` 全层（37 补丁）** | PRO 无对应文件，社区自研 |
| **影音直链 `DirectLinkOptions`** | strm 302 直链 + Follow302 + 客户端黑名单 —— **PRO 完全没有** |
| **`Core/` 适配层 11 类** | `EmbyVersionAdapter`/`FastReflection`/`HealthCheck`/`MemoryGuard`/`PerformanceMonitor` 等 |
| **内存运维** | `EnableMemoryAutoRestart`（倒计时）+ `EnableMemoryCleanup` + `RuntimeStatusList` —— 比 PRO 的 `MemoryOptions` 更全 |
| **豆瓣细分** | `DoubanCastChinese`/`DoubanPosterFallback`/`DoubanRatingOverride`/`DefaultNewLibraryTmdb` |
| **ISO 用户开关** | `EnableIsoExtraction`（PRO 内建无开关） |
| **播放会话实时监控** | `IntroSkip/PlaySessionMonitor` |
| **`ScrapeProxyWhitelist`** | 抓取代理白名单（PRO 无） |
| **独有 API** | `GetDoubanRatings`、`GetMultiVersionInfo` |

---

## 六、明确**不移植 / 不值得移植**（清理清单）

| 项 | 处理 | 理由（代码依据） |
|---|---|---|
| `Portable.Licensing` 授权全套 | ⛔ 不移植 | PRO 商业逻辑 |
| `BdInfoExaminer` / `BlurayMounter` 私有港 | ⛔ 不移植 | 私有/GPL；社区自研 UDF 选片已覆盖，语言/章节按 §七 自研补 |
| `MonoMod` 补丁框架 | ⛔ 不移植 | 与 Harmony 生态不互通 |
| `MemoryOptions` / `MemoryUsageLimitMB` / `SystemInfoItemList` | ⛔ 不移植 | 社区 `EnableMemoryAutoRestart` 等**更优** |
| `SuppressPluginUpdates` | ⛔ 不移植 | 社区主动移除（刻意决策） |
| `LastProxyCheckSucceeded` | ⛔ 不移植 | PRO 内部字段，社区 `ProxyServerStatus` 已覆盖 |
| `ShowMetadataExtraOptions` | ⛔ 不移植 | PRO 的 UI 折叠开关，无功能意义 |
| PRO 的"多选偏好+规则"UI 形态外壳 | ⛔ 不追 | 语义等价，改造成本高、收益近零 |

---

## 七、进度计划（按价值密度排序）

```
┌─────────────────────────────────────────────────────────────┐
│ 已完成 ✅                                                     │
│  · P1-1 搜索调优（6 项）                                      │
│  · P1-2 元数据刷新偏好（DefaultSeasonName / 图片刷新）        │
│  · P1-3 豆瓣 DoubanAccessToken                                │
│  · P1-4 诊断（得出 ISO 缺口结论）                             │
│  · 全量代码梳理（本文件）                                     │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 第 1 阶段 ★ 最高优先：ISO-1~4（strm/HTTP ISO 支持）           │
│  · 目标：让样本 253（DV FEL ISO）能探测出媒体信息             │
│  · 改动：4 个文件，零新依赖，守"不改 Path"红线                │
│  · 验收：重新探测后 Container/RunTimeTicks/流 不再为 None     │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 第 2 阶段：MPLS 语言/章节提取（Wiki#2）                       │
│  · 扩展 UdfTitleResolver 解析 STN_table + PlayListMark        │
│  · 纯自研，无新依赖                                           │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 第 3 阶段：双层杜比 P7 标注（Wiki#3）                         │
│  · 复用第 1 阶段的自定义 ffprobe 基建                         │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 第 4 阶段：ISO 截图/章节图（Wiki#5）                          │
│  · 复用 EnableImageCapture + CustomEncoderPath 补 ISO 分支    │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 第 5 阶段（可选 · 低风险）：G2 启动清 TMDB 缓存 / G4 追更秒   │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ 第 6 阶段（各自立项）：G3 跨库多版本进度（高风险）→ G1 截图后处理 │
└─────────────────────────────────────────────────────────────┘
```

**通用验收基线**
1. `dotnet build` 0 错误；ILRepack 产物部署两个 plugins 目录，MD5 一致。
2. Emby 重启：插件加载、补丁数正常、Healthy、0 error。
3. 新配置可见可改、重启持久化、三语无缺。
4. patch 类改动在 4.9.1.90 与 4.10.x 双版本各跑一次。

---

## 八、证据与可信度

| 结论 | 证据 | 等级 |
|---|---|---|
| PRO 全部 Options 属性清单 | `decompiled_v2/StrmAssistant/Options/**` 逐文件提取 | 源码实证（高） |
| 社区全部 Options 属性清单 | `StrmAssistant/Options/*.cs` 逐文件提取 | 源码实证（高） |
| 真缺口仅 4 项 | 双端属性集合差集（"PRO 有且社区无"） | 机器比对（高） |
| 搜索 6 项/`MarkPreviousEpisodesPlayed`/`FingerprintExcludeKeywords` 已实现 | `ModOptions.cs:19-85`、`AutoMarkWatched.cs:99`、`IntroSkipOptions.cs:99` | 源码实证（高） |
| PRO ISO 用 `bluray:"{0}"`+`--enable-libbluray` | `_pro_tmp/strings.txt:2045-2085,2419,2426,2437` | 反编译实证（高） |
| 社区 ISO 不覆盖 HTTP | `IsoMounter.cs:89-93` + P1-4 实测 | 源码+实测（高） |

**弱点**：形态差异项的"语义等价"为人工判定（已在 §2.1 标注）；G3 为高风险推断（需专项验证）。
