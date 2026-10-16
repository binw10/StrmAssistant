# StrmAssistant 社区版 × PRO 功能对比与优化移植方案（v6）

> **社区版底座**：`D:\下载\AI\StrmAssistant-myp015`（分支 myp015，版本 `3.0.55`，`net6.0`，基线 `mediabrowser.server.core 4.9.1.90`）
> **参照对象**：PRO `StrmAssistantPro.dll` 反编译源码 `D:\下载\AI\StrmAssistant.Releases\_pro_tmp\decompiled`（PRO 基线 **v3.0.0.55**，`netcoreapp6.0`）
> **方法**：① 双端 Options 属性名做**集合级机器比对**（PRO 202 项 / 社区 201 项）；② 关键模块逐文件读源码复核；③ 以你指定的 [Wiki·提取 ISO 媒体信息](https://github.com/sjtuross/StrmAssistant/wiki/提取-ISO-媒体信息) 为 ISO 能力的验收口径。
> **日期**：2026-10-16
> **与旧文档关系**：本 v6 在 v5 基础上**修正若干过期判断**、并把差异按「属性级证据」重新归类。`-v4.md`/`-终版.md`/`-v5.md` 可作为过程记录，**以本文件为准**。

---

## 〇、结论速览（先看这里）

| 维度 | 结论 |
|---|---|
| 补丁技术 | PRO = **MonoMod**（`MonoMod.Core`+`Mono.Cecil`）；社区 = **Lib.Harmony 2.4.2**（含 arm64 修复）。二者生态不互通，只能"按行为重写"，不能搬代码。 |
| 配置面对齐度 | 已对齐约 **80%**。PRO 202 项 vs 社区 201 项，去掉枚举/内部字段等噪声后，**真实缺口仅约 24 项**（见 §二）。 |
| 最大的"未对齐"集群 | ① **配置形态差异**（PRO 用"多选偏好+规则"范式，社区用"单值/多开关"）——占缺口一半以上，属**低风险可批量收敛**；② **ISO/BDMV 深度能力**（MLPS 语言提取、双层杜比 P7、changelog 级细节）；③ **分布式写回 API**。 |
| 社区已领先 PRO | 影音直链、ISO 用户开关、`Core/` 适配层（12 类）、播放会话实时监控、豆瓣辅助细分、内存自动重启等 —— **不要为了"对齐"而砍掉**。 |
| 优化后的移植策略 | **不追求 1:1 移植 PRO 代码**。改为三层：**L1 配置形态收敛**（批量、低风险）→ **L2 ISO 能力补齐**（按 Wiki 验收）→ **L3 重型能力**（每项独立立项）。详见 §五。 |

---

## 一、基石差异（决定"能不能直接搬代码"）

| 项 | PRO | 社区版 | 移植含义 |
|---|---|---|---|
| 目标框架 | `netcoreapp6.0` | `net6.0` | 等价，无碍 |
| 补丁技术 | **MonoMod**（`MonoMod.Core`/`MonoMod.Utils`/`Mono.Cecil`）+ 混合 Harmony | **Lib.Harmony** | ⛔ **最大障碍**：PRO 的 IL patch 代码不可移植，必须用 Harmony 等价重写 |
| Harmony 版本 | `0Harmony`（旧） | **2.4.2**（修 arm64 `NotImplementedException`） | 社区版更优，保留 |
| ISO/BDMV | `BlurayMounter`（含 BDInfo 移植，**私有**）+ `DiscUtils.Udf` | 自研 `IsoMounter`+`UdfTitleResolver` + `DiscUtils.Udf 0.16.13` | PRO 强依赖不可引（私有/GPL），用自研替代 |
| 元数据源 | `MovieDb`/`Tvdb`/`NfoMetadata` 插件 dll + `ToolGood.Words` + `SkiaSharp` + `ServiceStack.Text` | 原生 + `CHTCHSConv`+`TinyPinyin`+`SQLitePCL.pretty` | PRO 的 dll 引用是编译期绑定，社区走运行时反射更健壮 |
| 授权 | **Portable.Licensing**（License 文件 + SystemId 绑定 + 功能门控） | 无 | PRO 商业逻辑，**社区版无意义，不移植** |
| 配置页 | 分组：General/Network/Mod/Experience/IntroSkip(3 子页)/MediaInfo/Metadata(Build/Enhance/Refresh/Scrape)/Dashboard | 扁平聚合（11 个 Options 类） | PRO 的**分组信息架构**值得借鉴（可选做） |

**一句话**：两侧"功能语义"可比、"实现代码"不可搬。移植 = **按 PRO 的行为规格，用 Harmony + 社区现有基建重新实现**。

---

## 二、精确缺口矩阵（属性级机器比对，可信度：高）

### 2.1 真实缺口 · 按性质分三类

#### 🔴 A 类：功能性缺口（社区真没有，且有用户价值）

| # | PRO 属性 | PRO 语义（来自反编译） | 社区现状 | 优先级 |
|---|---|---|---|---|
| A1 | `LibraryScopePlayProgress` | 按媒体库分别记录多版本播放进度（仅剧集） | 无；`PresentationUniqueKey` 补丁只服务多版本合并 | **P3（高风险）** |
| A2 | `MarkPreviousEpisodesPlayed` + `MarkPreviousEpisodesPlayedThreshold`[5-95] | 看完当前集 → 自动把**前面几集**标已看，带阈值 | 有 `EnableAutoMarkWatched`+`AutoMarkWatchedThreshold`，但语义是"**当前集**达阈值即标记"，**不等价** | **P1** |
| A3 | `ImageCapturePostProcessMode` + `...ModeList` | 截图后处理（含 Dolby Vision 增强等） | 无（社区有 `ForcePortraitPoster`/`EnsureEpisode16x9`，属不同维度） | **P2** |
| A4 | `ClearRecentTmdbCacheOnStartup` | 启动时清 TMDB 最近缓存 | 无；社区有 `LocalTmdbCache` | **P2** |
| A5 | `DoubanAccessToken` / `DoubanClient` | 豆瓣新版 Token/客户端形态 | 只有 `DoubanCookie` | **P2** |
| A6 | `FingerprintExcludeKeywords` | 片头指纹提取**关键字**黑名单 | 有 `FingerprintBlacklistShows`（剧名黑名单），缺**关键字**维度 | **P1（小）** |
| A7 | `CatchupMinimumDelaySeconds` | 追更模式最小延迟（默认 10） | 有 `CatchupMinWaitMinutes`（分钟粒度） | **P2（可合并）** |

#### 🟡 B 类：配置形态缺口（功能在，但 PRO 是"多选偏好+规则"范式）

> 这是缺口的**主体**。社区把 PRO 的一个"多选偏好"拆成了多个 bool 开关或多个单值枚举。**功能大体可用，但 UI 形态与 PRO 不同**。

| # | PRO 属性 | 社区对应实现 | 形态差异 |
|---|---|---|---|
| B1 | `SearchTuningOptionList` + `SearchTuningPreferences`（6 项多选：DigitsAsTmdbId/ExcludeOriginalTitle/MixedChineseVariants/ShowMediaInfo/SuppressSearchSuggestions/TraditionalToSimplifiedChinese） | `EnhanceChineseSearchRestore`、`ExcludeOriginalTitleFromSearch`、`BlockSearchRecommend`（零散 bool） | 缺 `DigitsAsTmdbId`、`ShowMediaInfo`、`MixedChineseVariants` 三项；其余为"散开关"而非"统一多选" |
| B2 | `MetadataRefreshPreferences`+`MetadataRefreshRuleList`（规则：NoOverview/NoImage/NonChineseNameOverview/DefaultEpisodeName/DefaultSeasonName/ReplaceCapturedImage）+ `MetadataRefreshLibraryList/Scope` | `EpisodeRefreshScope`+`EpisodeRefreshOptionList`+`EpisodeRefreshLookbackDays`+`EpisodeRefreshLibraryList/Scope` | 语义重叠但命名/分组不同 |
| B3 | `PersonRefreshOptionList`+`PersonRefreshPreferences`（含 ExcludeAdultMetadata/DownloadImagesInAdvance 等） | `RefreshPersonOptionList`+`RefreshPersonMode` | 形态不同，PRO 项更细 |
| B4 | `BeautifyMultiVersion`+`MultiVersionDisplayPreference`+`MultiVersionReorderPreference` | `EnableMultiVersionEnhance`+`MultiVersionDisplayOption`+`MultiVersionSortOption` | 命名不同，粒度粗 |
| B5 | `EnforceUserPreferences`（含 scope 多值） | `EnforceUserPreferencesScope`+`EnforceLibraryOrder` | 社区拆成两处 |
| B6 | `MetadataCacheOptionList`（OptionList vs Mode 分离） | `MetadataCacheModeList`+`MetadataCacheMode` | 命名不同 |
| B7 | `IntroSkipFingerprintOptions`/`IntroSkipPlaybackOptions`/`IntroSkipScrapeOptions`（**嵌套子对象**） | 平铺在 `IntroSkipOptions` 同一层 | 信息架构差异 |
| B8 | `MultipleVersionsOptions`（嵌套对象） | 平铺在 `ExperienceEnhanceOptions` | 同上 |
| B9 | `IsMovieDbPluginSupported`/`IsTvdbPluginSupported`/`IsNfoMetadataPluginSupported`/`IsOriginalPosterSupported`/`IsOpenSubtitlesPluginSupported` | 社区为 `IsMovieDbPluginLoaded`/`IsTvdbPluginLoaded`/`IsNfoMetadataPluginLoaded` | 命名差 + 缺 `IsOriginalPosterSupported`/`IsOpenSubtitlesPluginSupported` |

#### ⚪ C 类：无需移植（PRO 专有 / 社区有意不移植）

| PRO 属性 | 理由 |
|---|---|
| `MemoryOptions`/`MemoryUsageLimitMB`/`SystemInfoItemList` | 社区用更丰富的 `EnableMemoryAutoRestart`+`EnableMemoryCleanup`+`RuntimeStatusList`，**已领先** |
| `SuppressPluginUpdates` | 社区 26.3.25 **主动移除**（刻意决策） |
| `LicenseWarningStatus`/授权相关 | PRO 商业逻辑 |
| `LastProxyCheckSucceeded` | PRO 代理状态内部字段，社区 `ProxyServerStatus` 已覆盖 |
| `ShowMetadataExtraOptions` | PRO 的 UI 折叠开关，无功能意义 |

### 2.2 反向：社区独有 / 领先 PRO（**禁止在对齐时丢失**）

| 模块 | 社区独有项 |
|---|---|
| 影音直链 | `DirectLinkOptions` 全区（`EnableStrmDirectRedirect`/`EnableLiveTvDirectRedirect`/`Follow302`/音视频客户端黑名单）—— PRO 完全没有 |
| ISO/BDMV | `EnableIsoExtraction` **用户开关** + 跨平台进程内 UDF（PRO 内建无开关） |
| 运维状态 | `EnableMemoryAutoRestart`+倒计时、`EnableMemoryCleanup`、`RuntimeStatusList` |
| 版本适配层 | `Core/` 全套 **12 类**：`EmbyVersionAdapter`/`EmbyApiVersion`/`FastReflection`/`ServiceLocator`/`PatchTracker`/`ConflictDetector`/`HealthCheck`/`MemoryCleaner`/`MemoryGuard`/`OptimizationAdvisor`/`PerformanceMonitor`/`PerformanceReporter` |
| 播放监控 | `IntroSkip/PlaySessionMonitor` 播放会话内**实时**片头/片尾标记 |
| 元数据细分 | `DoubanCastChinese`/`DoubanPosterFallback`/`DoubanRatingOverride`/`DoubanRatingOverride`、`DefaultNewLibraryTmdb`、`OptimizeMovieDbEpisodeScraping`、`DisableEpisodeImageScraping`、`ScrapeProxyWhitelist`、`IgnoreCertificateValidation` |
| 其它 | `GetDoubanRatings`/`GetMultiVersionInfo` API、`DoubanService`/`MultiVersionService`、`HasExtractionTimeout` 诊断只读项 |

> ⚠️ 注意：`EnableMemoryAutoRestart` 等**不在缺口里**——它们是社区增强，PRO 反而没有。

---

## 三、ISO/BDMV 专项对比（按你指定的 Wiki 口径）

Wiki「提取 ISO 媒体信息」列出的 **6 条能力**，逐条对照现状：

| Wiki 要求的能力 | PRO | 社区版现状 | 状态 |
|---|---|---|---|
| 1. 提取 ISO/BDMV 媒体信息、可记录播放进度 | ✅ | ✅ 媒体信息提取已通（`IsoExtraction` patch `MediaProbeManager.GetMediaInfoInternal` 的 `inputPath`） | 🟢 基本达成（播放进度记录见 A1） |
| 2. 从 **MPLS** 提取音轨语言、字幕语言、**章节** | ✅ | ⚠️ `UdfTitleResolver` 只解析 MPLS 选**正片**（时长求和取最长），**未提取语言/章节** | 🔴 **缺口（核心）** |
| 3. 检测**双层杜比 P7** 原盘、显示增强层 | ✅ | ❌ 无 | 🔴 缺口 |
| 4. 支持 本地路径 / http / **smb** 链接 | ✅ | ⚠️ 本地 `.iso` 通；http/smb 依赖外置 rffmpeg（无内置） | 🟡 部分 |
| 5. 自定义 ffmpeg 支持 ISO 视频截图与**章节图** | ✅ | ⚠️ `CustomEncoderPath` 已有，但 ISO 截图/章节图未验证 | 🟡 部分 |
| 6. 支持 **DVD ISO** 且自动识别 DVD/蓝光 | ✅ | ✅ `UdfTitleResolver.ResolveDvd`（`VTS_01_1.VOB`→最大 VOB）+ `IsBluray` 判定 | 🟢 已实现（待真碟验证） |

**机制差异（必须理解，决定实现路线）**

| 维度 | PRO | 社区版 |
|---|---|---|
| 原理 | Harmony **Transpiler** 把 Emby 托管文件系统 `IFileSystem.OpenRead(path)` 替换为进程内 `UdfReader.OpenFile`，**让 Emby 自己读 UDF**；再用 `BdInfoExaminer`(=Emby 内置 BDInfo 港口)解析碟片 | Harmony **Prefix** 只替换 ffprobe 的 `inputPath`；Windows 用 PowerShell `Mount-DiskImage` 挂载，Linux/Docker 交由自定义 ffprobe 直读 `.iso` |
| 正片选择 | 复用 Emby 内置 `BdInfoExaminer.GetDiscInfo`（**最准**） | 自研轻量 MPLS 解析（playlist 内 PlayItem 时长**求和**取最长） |
| 平台 | Windows/Linux/**Docker**/macOS 全通 | Windows 挂载；Linux/Docker 需自定义 ffprobe |
| 安全副作用 | 改 `MediaSourceInfo.Container="iso"` | **只改探测路径，不动 `Path`/条目身份**（更保守） |
| 用户开关 | 无（内建） | `EnableIsoExtraction`（默认 OFF） |

**结论**：社区版 ISO 已解决 **最能落地的一步**（跨平台取到媒体信息 + 选正片）；剩余缺口集中在 **MPLS 深度信息（语言/章节）**、**双层杜比 P7**、**http/smb 内置支持**——这三项恰好是 Wiki 里 PRO 标注最多的部分。

---

## 四、移植工程红线（沿用，全部仍有效）

1. 技术栈锁 `net6.0`，**不引** PRO 私有强依赖（`BlurayMounter`、`MonoMod`、`Portable.Licensing`）。
2. 新增依赖须为 **MIT/BSD/Apache**，并同步 `csproj` 的 **ILRepack 合并参数**（当前合并 7 个 dll：`0Harmony`/`ChineseConverter`/`TinyPinyin`/`DiscUtils.Core`/`Streams`/`Udf`/`Iso9660`），否则运行时缺 dll。
3. `Resources.resx` + `.zh.resx` + `.zh-hant.resx` + `Resources.Designer.cs` **四处必须同步**（缺 Designer 属性 → 配置页 500/空白）。
4. 所有写回类改动（豆瓣/缓存/媒体信息）一律「**仅补空 + 字段级合并**」，禁覆盖用户数据。
5. 枚举本地化不得依赖 Emby 4.10 的 `EnumExtensions.GetDescription`，用与基类匹配的 `DescriptionAttribute` 查找。
6. `MediaSourceInfo.Path` **不可改写**（保护播放路径与条目身份）。
7. 版本适配统一走 `Core/EmbyVersionAdapter`，禁止散落硬编码版本判断。

---

## 五、优化后的移植方案（分层推进）

> **策略调整**：不再"逐项追 PRO changelog"，改为**按价值/风险分层**。每层内部按依赖排序。

### L1 · 配置形态收敛（低风险、可批量、收益直观）

目标：把 B 类缺口按 PRO 范式归并，**保留旧配置可读**（迁移：读旧键→写新键，一次性）。

| 顺序 | 项 | 做法 | 风险 |
|---|---|---|---|
| L1-1 | **A6 `FingerprintExcludeKeywords`** | `IntroSkipOptions` 加关键字黑名单（照 `FingerprintBlacklistShows` 范式），三语 resx | 极低 |
| L1-2 | **B1 搜索调优归一** | 新增 `SearchTuningPreferences`（多选，6 项）+ `SearchItemTypeList`；补齐 `DigitsAsTmdbId`、`ShowMediaInfo`、`MixedChineseVariants`；旧 3 个 bool 做**读兼容** | 低 |
| L1-3 | **B4 多版本美化细分** | `EnableMultiVersionEnhance` 拆为 `BeautifyMultiVersion`+`MultiVersionDisplayPreference`+`MultiVersionReorderPreference` | 低 |
| L1-4 | **B2/B3 刷新偏好多选化** | `MetadataRefresh*`/`PersonRefresh*` 归并为"偏好列表+规则列表"，旧单值读兼容 | 低-中 |
| L1-5 | **B9 支持性只读项补齐** | 补 `IsOriginalPosterSupported`/`IsOpenSubtitlesPluginSupported`，命名对齐 | 极低 |
| L1-6 | **A7 `CatchupMinimumDelaySeconds`** | 与 `CatchupMinWaitMinutes` 合并（秒粒度） | 极低 |
| L1-7 | **A5 豆瓣 Token/Client** | `DoubanCookie` 旁增 `DoubanAccessToken`/`DoubanClient` | 低 |
| L1-8 | **A4 `ClearRecentTmdbCacheOnStartup`** | 启动钩子按需清 `LocalTmdbCache` | 低 |

**L1 交付物**：一轮 `dotnet build` + 部署验证；配置页可见可改、重启持久化、三语无缺。

### L2 · ISO/BDMV 能力补齐（按 Wiki 验收，高价值）

| 顺序 | 项 | 做法 | 依赖 |
|---|---|---|---|
| L2-1 | **MPLS 语言/章节提取**（Wiki#2） | 扩展 `UdfTitleResolver.ParsePlaylist`：在已解析的 PlayItem 基础上，继续解析 **STN 流表**（`STN_table` → `stream_entry`/`stream_attributes`）取音轨/字幕语言码（ISO-639），解析 `PlayListMark`（章节标记，45kHz→ms） | 纯自研，无新依赖 |
| L2-2 | **双层杜比 P7 检测**（Wiki#3） | 通过自定义 ffprobe 探测 `dv_bl_signal_compatibility_id`/增强层流，标注到媒体信息 | 需自定义 ffprobe（Wiki 已要求） |
| L2-3 | **http/smb ISO 内置支持**（Wiki#4） | PRO 靠 `BdInfoExaminer`+`UdfReader(Stream)` 直读 URL；社区先在 `UdfTitleResolver` 增 `ResolveFromStream`，再定是否内置 http 拉流 | 中-高（网络流 UDF 随机读） |
| L2-4 | **ISO 截图/章节图验证**（Wiki#5） | 复用 `ImageCapture` + `CustomEncoderPath`，补 ISO 输入路径分支 | 需实测 |
| L2-5 | **真碟回归**（承接 v5 遗留） | 多分支/多 m2ts 蓝光 ISO 选片正确性；AACS/损坏 ISO 静默回退；x64/arm64 双端 | 需你提供样片 |

> **L2-1 是本层最高性价比**：纯自研、无新依赖、直接命中 Wiki 第 2 条，且与现有 MPLS 解析代码同源。

### L3 · 重型能力（每项独立立项、充分回归）

| 项 | 风险 | 立项前提 |
|---|---|---|
| **A1 `LibraryScopePlayProgress`**（跨库多版本进度） | 🔴 改 `PresentationUniqueKey` 计算链路，直接影响播放进度/收藏键 | 需专项风险评估；先在测试库验证键变更无数据迁移副作用 |
| **A3 `ImageCapturePostProcess`**（含 DV 增强） | 🟡 依赖自定义 ffmpeg + 图像后处理管线 | 等 Wiki#5 验证后再做 |
| **分布式写回 `POST /Items/SyncMediaInfo`** | 🟡 社区已有 `CustomProbePath`→rffmpeg 路线 | **先决策走哪条路**再动手（rffmpeg vs 中央调度 API） |
| **JS 注入套件拆分**（PRO 11 个端点 vs 社区单入口） | 🟢 性能优化 | 按需 |
| **A2 `MarkPreviousEpisodesPlayed`** | 🟡 与 `EnableAutoMarkWatched` 语义需并存/合并 | 建议合并为统一"自动标记"配置组 |

### ⛔ 明确不移植

- `SuppressPluginUpdates`（社区主动移除）
- `MemoryOptions`/`SystemInfoItemList`（社区实现更优）
- 授权/`Portable.Licensing` 全套
- `BlurayMounter`/`MonoMod` 直接依赖

---

## 六、执行顺序与里程碑

```
L1-1 → L1-2 → L1-3 → L1-4 → L1-5~L1-8        [1 轮构建回归]
   ↓
L2-1（MPLS 语言/章节）★ 最高性价比            [独立提交 + 真实 ISO 验证]
   ↓
L2-5（真碟回归，含 v5 遗留）                   [需样片]
   ↓
L2-2 → L2-4 → L2-3                            [依赖自定义 ffprobe]
   ↓
L3: A2 合并 → 分布式写回决策 → A1 专项 → A3    [各自立项]
```

**每阶段验收基线（通用）**
1. `dotnet build` 无错误；ILRepack 合并产物部署到 `StrmAssistantPluginOutputDir`（默认 `D:\Downloads\embyserver\programdata\plugins`，`/p:` 可覆盖）。
2. x64 与 arm64 双端启动日志无 `Harmony not implemented` / `GLIBC not found`；`RuntimeStatusList` 各模块状态正常。
3. 新增配置项后台可见可改、重启持久化、三语资源无缺。
4. 逐项回归：改配置→重启→验证行为→回滚配置→验证恢复。
5. patch/Transpiler 类改动须在 **4.9.1.90 与 4.10.x** 双版本各跑一次。

---

## 七、附录 · 证据与可信度

| 结论 | 证据 | 等级 |
|---|---|---|
| PRO 基线 v3.0.0.55、MonoMod 技术栈 | `_pro_tmp/decompiled/StrmAssistantPro.csproj`、`AA---------/A----------.cs` | 反编译实证 |
| 缺口矩阵（§二） | 双端 Options 属性名 `comm` 集合比对：PRO 202 vs 社区 201，逐条归属 A/B/C 类 | **机器比对（高）** |
| PRO 无用户 ISO 开关、依赖 BlurayMounter | 反编译 `Options/*` grep 0 命中；csproj `<Reference Include="BlurayMounter">` | 反编译实证 |
| PRO ISO 用 `BdInfoExaminer`+Transpiler `OpenRead` | `A----------.cs` 中 `CodeMatcher.MatchStartForward(...OpenRead...)` + `BdInfoExaminer.GetDiscInfo` | 反编译实证 |
| 社区 ISO 走挂载/进程内 UDF | `Common/IsoMounter.cs`、`Common/UdfTitleResolver.cs`、`Mod/IsoExtraction.cs` | 源码实证 |
| Wiki 6 条能力 | 你提供的 Wiki 页面原文 | 一手资料 |
| `EmbyApiVersion` 数值序 | 已含 `V4_10 = 5000000`（`Core/EmbyApiVersion.cs`），v4 文档所述 bug **已修复** | 源码实证 |
| 社区独有项（§2.2） | `Mod/*.cs`、`Core/*.cs`、`Web/*` 文件名与内容 | 源码实证 |

**可信度说明**
- §二缺口矩阵：**高**（属性级机器比对）。弱点：PRO"改名但功能等价"项已归入 B 类并标注形态差异，不会误判为真缺口。
- §三 ISO 机制：**高**（反编译+源码双向实证）。
- A2 语义差异（`MarkPreviousEpisodesPlayed` vs `EnableAutoMarkWatched`）：**中**（行为语义推断，已在表内标注）。
- 社区 ISO 的"M2TS 选片精度"：**中**（自研 MPLS 求和法在合成盘验证通过，真碟待 L2-5）。

---

## 八、与旧文档的差异说明（修正记录）

| 旧文档判断 | v6 修正 |
|---|---|
| v5 称 `ClearEmbeddedMetadata` "PRO 有、社区无" | ✅ 社区**已实现**（独立开关，默认 ON），不算缺口 |
| v5 称 `EnableAutomaticIntroScraping` 缺口 | ✅ 社区**已实现**（默认 OFF 与 PRO 对齐） |
| v5 §二 #5/#6 反复修正 | 本版以机器比对为准，不再逐条人工判断 |
| v4 称 `EmbyApiVersion` 数值序 bug（41000 < 49190） | ✅ **已修复**（现 `V4_10=5000000`） |
| v4 称 ISO"未做且主动排除" | ⚠️ **已过期**：P0/P1-1 已落地跨平台 UDF + 正片解析 |
| v4 称"音乐持久化未做" | ✅ 实为"开关未开"，已复核 |
| 各旧文档缺口条目数不一 | 本版统一为**机器比对口径**：真缺口 ~24 项（A 7 + B 9 + 其他） |
