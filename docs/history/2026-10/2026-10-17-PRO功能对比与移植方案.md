# PRO 功能对比与移植方案 —— 最终版（v10）

> **基准**：PRO `StrmAssistantPro.dll` **3.0.0.55** 反编译树 `d:\Downloads\ai\StrmAssistant\.decomp\pro`
> （`Properties/AssemblyInfo.cs` 实证 `AssemblyVersion = 3.0.0.55`，`netcoreapp6.0` / MonoMod）
> **对照方**：社区版 `d:\Downloads\ai\StrmAssistant\StrmAssistant\`（分支 `myp015`，`net6.0` / Lib.Harmony，`AssemblyVersion 3.0.55`）
> **方法**：**直接读代码**。每条结论都附「PRO 侧 file:line」+「社区侧 file:line（或 grep 0 命中证明缺失）」。
> **日期**：2026-10-16　**状态**：取代 `PRO功能差异全量梳理与移植计划-v8.md` 与 `PRO更新日志对照与移植优化方案-v9.md`，为当前唯一权威基线。

---

## 〇、一页看懂

| 层面 | 结论 |
|---|---|
| **整体功能对齐度** | 约 **95%**。PRO 的功能社区版基本都有，剩下的都是「锦上添花」级别 |
| **已移植完成** | P0 两项配置、ISO 全链路（本地 + 远程）、MPLS 语言/章节、双层杜比 P7、ISO 截图/章节图、阶段 0 两项（豆瓣客串、多版本画质+体积） |
| **尚未移植完毕** | **6 项**（R1~R6），全部低优先，**不做也不影响主用** |
| **仅需验证、不需移植** | **2 项**（V1 搜索特殊符号、V2 MovieDb 1.9.9.0 兼容） |
| **绝不能动** | 社区自研 `Mod/` 39 补丁 + `Core/` 11 类 + `DirectLinkOptions` 直链 + 内存运维 —— **社区反超面** |
| **红线** | 不改 `MediaSourceInfo.Path`；不引 PRO 私有依赖（`BlurayMounter`/`BdInfoExaminer`/`Portable.Licensing`/MonoMod）；三份 resx + `Resources.Designer.cs` 四处同步 |

> **一句话**：能力面已高度对齐，**唯一真正的大工程（ISO/strm 全链路）已完成并实机验证**；剩余 6 项按需做即可。

---

## 一、已移植完成清单（代码实证）

| # | 功能 | PRO 锚点 | 社区落点 / 提交 | 状态 |
|---|---|---|---|---|
| C1 | 内嵌元数据清除开关 `ClearEmbeddedMetadata` | PRO 隐藏项（`.50`） | `Options/MediaInfoExtractOptions.cs`；`a48a2cd` | ✅ 已落地 |
| C2 | 自动刮削片头片尾开关 `EnableAutomaticIntroScraping`（默认 OFF，对齐 PRO） | PRO `.53` | `Options/IntroSkipOptions.cs`；`a48a2cd` | ✅ 已落地 |
| C3 | ISO/BDMV 本地媒体信息提取（跨平台进程内 UDF + Windows 原生挂载双路径） | PRO 进程内 UDF + `IFileSystem.OpenRead` patch | `Common/UdfTitleResolver.cs`、`Common/IsoMounter.cs`、`Mod/IsoExtraction.cs`；`a48a2cd`/`06c9399` | ✅ 已落地 |
| C4 | 远程/strm HTTP ISO 探测 | PRO `.48/.49` `.iso.strm` 系列 | `IsoMounter.TryNormalizeRemoteIso`([L207-230](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Common/IsoMounter.cs#L207-L230))、`GetRemoteProbePath`([L242-278](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Common/IsoMounter.cs#L242-L278))、`IsoExtraction` 远程分支([L154-173](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Mod/IsoExtraction.cs#L154-L173))；`1741f6d` | ✅ 已落地 |
| C5 | MPLS 音轨/字幕语言 + 章节解析 | PRO `PLAYLIST/*.mpls` 解析 | `Common/UdfTitleResolver.cs`（STN_table / PlayListMark）+ `IsoExtraction` 章节补写([L211-265](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Mod/IsoExtraction.cs#L211-L265))；`1741f6d`/`c8e554c` | ✅ 已落地 |
| C6 | 双层杜比 Profile 7 标注 | PRO `.53` HDR Vivid 检测 | `Common/DvProfileDetector.cs`、`Mod/DolbyVisionProfile.cs`；`c5dc966` | ✅ 已落地 |
| C7 | ISO 截图 / 章节图 | PRO `.49` http ISO 截图 | `Mod/EnableImageCapture.cs.RewriteIsoInput`([L482-530](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Mod/EnableImageCapture.cs#L482-L530)) 本地挂载 + 远程窗口双分支；`6adc50f`/`c8e554c` | ✅ 已落地 |
| C8 | 豆瓣客串演员中文化 | PRO `.55` S5 修复 | `Provider/Douban/DoubanAssist.cs:636` `Actor \|\| GuestStar`；`eaf5955` | ✅ 已落地 |
| C9 | 多版本「画质+体积」显示格式 `MediaInfoExtended` | PRO `.55` S3 新增枚举 | `Options/MultipleVersionsOptions.cs:25-26`、`Web/Service/MultiVersionService.cs:124`、三 resx + Designer；`eaf5955` | ✅ 已落地 |
| C10 | 仪表盘搜索索引「重建」 | PRO `.55` | `Mod/EnhanceChineseSearch.cs:60 ScheduleIndexRebuild`、`Options/View/HomePageView.cs:169-170` | ✅ 已落地 |
| C11 | 挂载释放缺陷修复（清单持久化/启动对账/关闭期释放） | — | `Common/IsoMountEntryPoint.cs`；`eaf7432`/`9bd8f4e` | ✅ 已落地 |

---

## 二、**尚未移植完毕**清单（最终版 · 6 项）

> 排序按「价值密度」。全部为低优先，逐项附 PRO 锚点 + 社区现状证据 + 落点建议。

### R1 · 筛选菜单 `HasDoubanId` / `HasImdbId`　【P2 · 低风险】

- **PRO 实现**：新增静态端点 `StrmAssistant.Web.Api\GetFilterMenuHtml.cs:10`
  路由 `[Route("/{Web}/modules/filtermenu/filtermenu.template.html", "GET", IsHidden = true)]` + `[Unauthenticated]`；
  由 `StrmAssistant.Web.Service\ScriptInjectService.cs:151-154` 返回**覆写后的筛选菜单模板资源**，
  模板内注入「有无豆瓣 ID / 有无 IMDb ID」两个筛选项。（模板为内嵌资源，非 `.cs`，故 `HasDoubanId` 字面量不在反编译 `.cs` 中）
- **社区现状**：**ABSENT**。全仓 `grep HasDoubanId|HasImdbId`（含 `.js`/`.resx`/`.html`）= **0 命中**。
- **差距**：库内按「有无豆瓣/IMDb 匹配」筛选的能力缺失。
- **落点建议**：
  1. 新增 `Web/Api/GetFilterMenuHtml.cs`（同 PRO 路由，覆写 `filtermenu.template.html`）；
  2. 嵌入改过的模板资源（在原 Emby 模板基础上追加两个 filter 项）；
  3. `Web/Helper/ShortcutMenuHelper.cs` 侧无需改数据，只做模板覆盖。
- **验收**：Emby 网页版筛选菜单出现「有豆瓣匹配 / 有 IMDb 匹配」；勾选后列表结果正确；不动任何数据。

### R2 · 搜索索引「优化 / 空间回收」`OptimizeSearchIndex`　【P2 · 低风险】

- **PRO 实现**：`A----------\A----------.cs:24304` 私有 `OptimizeSearchIndex()`，底层静态实现 `A----------\A----------.cs:30514`
  `internal static (long Reclaimed, long Bytes)? OptimizeSearchIndex()` —— 返回**回收页数 / 字节数**；
  仪表盘文案资源键 `Dashboard_Search_Index_Reclaimed`（`StrmAssistant.Properties\Resources.cs:126`）。
- **社区现状**：只有**重建**、无**回收**。`Mod/EnhanceChineseSearch.cs:60 ScheduleIndexRebuild()`（清标记文件，下次重启重建）；
  仪表盘 `Options/View/HomePageView.cs:169-170 case "ScheduleIndexRebuild"`；按钮 `Options/GeneralOptions.cs:52-55`。
  `grep OptimizeSearchIndex` 社区 = **0 命中**。
- **差距**：缺少「不重建、只回收索引空间」的低成本优化入口。
- **落点建议**：`Mod/EnhanceChineseSearch.cs` 新增 `OptimizeSearchIndex()`（对标 PRO 返回 `(Reclaimed, Bytes)`）；
  `Options/View/HomePageView.cs:169` 旁加 `case "OptimizeSearchIndex"`；`Options/GeneralOptions.cs` 旁加按钮 + 三 resx 文案。
- **验收**：点击后仪表盘显示回收页数/字节；索引功能不受影响。

### R3 · 截图后处理多选 `ImageCapturePostProcessMode`　【P3 · 中】

- **PRO 实现**：`StrmAssistant.Options.MediaInfo\MediaInfoExtractOptions.cs:307-341`
  —— `[EditMultilSelect]` + `[SelectItemsSource("ImageCapturePostProcessModeList")]` 的 `string ImageCapturePostProcessMode`；
  数据源 `ImageCapturePostProcessModeList`（`List<EditorSelectOption>`，同文件 `:309` 定义，`:897/:1070` 填充）；
  消费点在混淆文件 `A----------\AA---------.cs:15306 / 15677`。描述键：`...Generate_additional_images__Default_is_OFF_`。
- **社区现状**：有截图能力（`Mod/EnableImageCapture.cs` + `Options/MediaInfoExtractOptions.cs` 的 `EnableImageCapture`），
  但 `grep ImageCapturePostProcessMode` = **0 命中** → 无「截图后额外生成海报/背景图」的多选后处理。
- **差距**：仅影响「无主图时自动截图」的附加产物；不截图则完全无感。
- **落点建议**：`MediaInfoExtractOptions` 加 `ImageCapturePostProcessMode` + `ModeList`；`EnableImageCapture` 出图后
  按选中模式生成附加图（含 16:9 / 2:3 / 背景图等）；三 resx + Designer 同步。
- **验收**：勾选模式后截图流程额外产出对应图像；不勾选行为不变。

### R4 · 启动清 TMDB 最近缓存 `ClearRecentTmdbCacheOnStartup`　【P3 · 小】

- **PRO 实现**：配置项 `StrmAssistant.Options.Metadata\MetadataEnhanceOptions.cs:748`；
  启动消费点 `StrmAssistant\Plugin.cs:1772`（默认 `false`）/ `:1796`（为真时清缓存）。
- **社区现状**：**ABSENT**（`grep ClearRecentTmdbCacheOnStartup` = 0 命中）。已有本地缓存机制
  `Mod/LocalTmdbCache.cs:13-28`（手动机制 + 缓存自动过期）。
- **差距**：无「启动时自动清最近缓存」的开关（社区靠过期兜底，通常够用）。
- **落点建议**：`Options` 加开关 → 启动钩子里调用 `LocalTmdbCache` 清理最近区段即可。
- **验收**：开启后重启日志显示缓存已清；关闭无副作用。

### R5 · 追更最小延迟秒粒度 `CatchupMinimumDelaySeconds`　【P3 · 极小】

- **PRO 实现**：`StrmAssistant.Options\GeneralOptions.cs:112`，且 **`[Browsable(false)]`（隐藏项，非用户可见配置）**；
  消费 `AA---------\A----------.cs:40666` `TimeSpan.FromSeconds(generalOptions.CatchupMinimumDelaySeconds)`；日志点 `A----------\A----------.cs:14225`。
- **社区现状**：`Options/GeneralOptions.cs:26-30` `CatchupMinWaitMinutes`（**分钟**粒度，用户可见）；
  消费 `Common/QueueManager.cs:168`。
- **判定**：**功能已等价**，仅刻度不同，且 PRO 该项本身是隐藏项 → 价值最低。
- **落点建议**：**可不做**。若要做，仅在 `GeneralOptions` 加隐藏 `CatchupMinimumDelaySeconds`，由 `QueueManager` 换算。
- **验收**：若做，秒级设置生效；不做则记录为「刻意不对齐」。

### R6 · 跨库多版本播放进度 `LibraryScopePlayProgress`　【P4 · 高风险 · 单独立项】

- **PRO 实现**：配置项 `StrmAssistant.Options.Experience\ExperienceEnhanceOptions.cs:399-403`
  （`UIFunctionOptions_LibraryScopePlayProgress_Separate_Play_Progress_by_Library`）；
  消费 `AA---------\A----------.cs:2896`、`A----------\A----------.cs:41823`。
- **社区现状**：**ABSENT**（`grep LibraryScopePlayProgress` = 0 命中）。
- **风险**：实现必须改 `PresentationUniqueKey` 链路 → **直接影响播放进度键**，可能造成进度错乱/丢进度。
- **落点建议**：**单独立项**，先写专项验证（双库同剧集多版本、进度互不串味、升级/降级回归），验证通过再动。
- **验收**：两库同剧集各自独立进度；已有进度不丢；关闭时行为与现状完全一致。

---

## 三、仅需**验证**、不需移植（2 项）

| # | 项 | PRO 侧 | 社区现状 | 动作 |
|---|---|---|---|---|
| **V1** | 中文搜索特殊符号处理 | 符号逻辑在**混淆/加密段内，无法逐字还原**（`grep 'Replace\("[^A-Za-z0-9]'` 在 PRO 树 0 命中） | `Mod/EnhanceChineseSearch.cs`：索引侧([L88-92](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Mod/EnhanceChineseSearch.cs#L88-L92))剥离 `'`/`.`/`·`/`-`；查询侧([L112-114](file:///d:/Downloads/ai/StrmAssistant/StrmAssistant/Mod/EnhanceChineseSearch.cs#L112-L114))剥离 `·`/`-` | **不照搬 PRO**（不可还原）。改为**语料驱动**：从真实库名采样含符号条目，比对加符号前后命中率，**只补有实测收益的符号**，并用回归条目集确认无误命中 |
| **V2** | 适配 MovieDb 1.9.9.0 | PRO 硬绑定反射目标 | 社区走**反射 + 版本分层**（`Options/MetadataEnhanceOptions.cs:104 MovieDbVersionTier`、`Mod/ChineseMovieDb.cs:93` 消费该分层），天然容错 | 在真实 Emby 环境**跑一次 1.9.9.0 验证**，把 `MovieDbVersionTier` 落进启动日志；无需改代码 |

---

## 四、ISO 专项残留（非缺口，属已知限制）

| 项 | 现状 | 证据 |
|---|---|---|
| 本地 ISO 章节补写 | ✅ 已通 | `Mod/IsoExtraction.cs:185-189` + Postfix `:235-265` |
| **远程/strm ISO 章节** | ⚠️ **刻意不补写**（头部窗口被截断，DiscUtils 无法构造 UDF 读取器，实测 `mpls` 不在窗口内） | `Mod/IsoExtraction.cs:154-159` 注释 |
| 真碟多分支/多 m2ts 选片 | ⏳ **待真实蓝光 ISO 样片验证** | `Common/UdfTitleResolver.cs` 按 playlist 内全部 PlayItem **求和**选最长 → 最长 PlayItem 的 5 位号（设计上已规避分支选错） |
| Linux/Docker 蓝光 ISO 直读 | ⏳ 待验证（ffmpeg `dvd-iso` demuxer 只面向 DVD；若不能直读，则「临时解出 m2ts」由可选升为必做） | `Common/FfmpegPathOverride.cs` 能力位 |

---

## 五、执行顺序（建议）

```
┌────────────────────────────────────────────────────────────┐
│ ✅ 已完成                                                   │
│  · P0 两项配置 / ISO 全链路（本地+strm）/ MPLS 语言章节      │
│  · 双层杜比 P7 / ISO 截图章节图 / 挂载释放修复               │
│  · 阶段 0：豆瓣客串 + 多版本「画质+体积」                    │
└────────────────────────────────────────────────────────────┘
                         ↓ （以下按需，均低优先）
┌ 阶段 A · 低风险省钱项（P2，可先做） ─────────────────────────┐
│  · R2 搜索索引「优化/回收」（对标 PRO OptimizeSearchIndex）   │
│  · R1 筛选菜单 HasDoubanId / HasImdbId                       │
└──────────────────────────────────────────────────────────────┘
                         ↓
┌ 阶段 B · 小配置项（P3） ─────────────────────────────────────┐
│  · R4 ClearRecentTmdbCacheOnStartup                          │
│  · R5 CatchupMinimumDelaySeconds（可不做，功能已等价）       │
│  · R3 ImageCapturePostProcessMode（截图后处理多选）          │
└──────────────────────────────────────────────────────────────┘
                         ↓
┌ 阶段 C · 高风险项（P4，单独立项） ───────────────────────────┐
│  · R6 LibraryScopePlayProgress —— 先专项验证 PresentationUniqueKey │
└──────────────────────────────────────────────────────────────┘
                         ↓
┌ 阶段 D · 验证型（随时） ─────────────────────────────────────┐
│  · V2 MovieDb 1.9.9.0 实机验证                              │
│  · V1 搜索特殊符号语料驱动补漏                               │
│  · ISO 残留：真碟多分支选片 / Linux 蓝光 ISO 直读            │
└──────────────────────────────────────────────────────────────┘
```

---

## 六、移植工程红线（必须遵守）

1. 技术栈锁 **`net6.0`**；不引 PRO 私有依赖（`BlurayMounter` / `BdInfoExaminer` / `Portable.Licensing` / `MonoMod`）。
2. 新增第三方依赖须 MIT/BSD/Apache，并**同步 `csproj` 的 ILRepack 合并参数**。
3. **三份 resx（`Resources.resx` / `.zh.resx` / `.zh-hant.resx`）+ `Properties/Resources.Designer.cs` 四处必须同步** —— `DisplayNameL`/`DescriptionL` 是**反射查 `Resources` 上同名 public static string 属性**，漏改 Designer 会导致**配置页整页空白**（前端 500）。
4. **不得改写 `MediaSourceInfo.Path`**（播放路径与条目身份红线）；探测输入改写只改 ffprobe 入参。
5. 枚举本地化用基类 `DescriptionAttribute`，不依赖 Emby 4.10 的 `EnumExtensions.GetDescription`（4.10 改精确类型索引，子类特性会失配）。
6. 版本适配统一走 `Core/EmbyVersionAdapter`。
7. 插件部署后**必须重启 Emby** 才加载新 DLL。

---

## 七、明确**不移植**清单（清理项）

| 项 | 理由 |
|---|---|
| `Portable.Licensing` 授权全套（`LicenseText`/`LicenseWarningStatus`/`ShowLicenseOptions`/`MITLicenseText`/`Part1`/`Part2`） | PRO 商业逻辑 |
| `BdInfoExaminer` / `BlurayMounter` 私有港 | 私有/GPL；社区自研 UDF 选片 + MPLS 解析已覆盖 |
| `MonoMod` 补丁框架 | 与 Harmony 生态不互通 |
| `MemoryOptions` / `MemoryUsageLimitMB` / `SystemInfoItemList` | 社区 `EnableMemoryAutoRestart`/`EnableMemoryCleanup` 更优 |
| `SuppressPluginUpdates` / `UpdatePluginTask` | 社区主动删除（自更新有风险，`170cd45`） |
| `LastProxyCheckSucceeded` / `ShowMetadataExtraOptions` | PRO 内部态/UI 折叠开关，无功能意义 |
| `SyncMediaInfo`（分布式写回） | 需自建写回协议，风险高，当前无诉求 |
| PRO 的「多选偏好+规则」UI 外壳（`MetadataRefreshPreferences+RuleList`、`SearchTuningOptionList` 等） | 与社区 bool/单值枚举**语义等价**，改造回归面大、收益近零 |
| `EnableAutomaticDecompression` | 社区 `AltMovieDbConfig.cs:145` 已硬编码 `GZip\|Deflate` |

---

## 八、绝不能为"对齐"误删的**社区反超面**

| 模块 | 说明 |
|---|---|
| `Mod/` 全层（39 补丁） | PRO 无对应文件，社区自研 |
| `Core/` 适配层（11 类：`EmbyVersionAdapter`/`FastReflection`/`HealthCheck`/`MemoryGuard`/`PerformanceMonitor`/`ConflictDetector`…） | 社区自研 |
| 影音直链 `DirectLinkOptions`（strm 302 + Follow302 + 客户端黑名单 + LiveTv 直链） | **PRO 完全没有** |
| 内存运维（`EnableMemoryAutoRestart` 倒计时 + `EnableMemoryCleanup` + `RuntimeStatusList`） | 比 PRO `MemoryOptions` 更全 |
| 豆瓣细分（`DoubanCastChinese`/`DoubanPosterFallback`/`DoubanRatingOverride`/`DefaultNewLibraryTmdb`） | 社区独有 |
| ISO 用户开关 `EnableIsoExtraction` | PRO 内建无开关 |
| 独有 API（`GetDoubanRatings`/`GetMultiVersionInfo`）、`IntroSkip/PlaySessionMonitor`、`ScrapeProxyWhitelist` | 社区独有 |

---

## 九、验收基线（通用）

1. `dotnet build StrmAssistant\StrmAssistant.csproj -c Release` **0 错误**；ILRepack 产物部署 plugins 目录。
2. Emby **重启**后：插件加载、补丁数正常、`Healthy`、日志 **0 error**。
3. 新配置项**可见可改、重启持久化、三语无缺**（用反射取 `Resources` 同名属性验证，**不能靠扫 DLL 字符串**）。
4. patch 类改动在 **4.9.1.90 与 4.10.x 双版本**各跑一次。

---

## 十、证据表（本轮直接读代码所得）

| 结论 | 证据 | 等级 |
|---|---|---|
| PRO = 3.0.0.55 | `.decomp\pro\Properties\AssemblyInfo.cs` `AssemblyVersion=3.0.0.55` | 实证（高） |
| 社区**无**筛选菜单 `HasDoubanId/HasImdbId` | 全仓 `grep` **0 命中**；PRO 端点 `Web.Api\GetFilterMenuHtml.cs:10` 路由 + `Web.Service\ScriptInjectService.cs:151-154` | 实证（高） |
| 社区**只有重建、无回收** | `Mod/EnhanceChineseSearch.cs:60`（Rebuild）、`Options/View/HomePageView.cs:169-170`；`grep OptimizeSearchIndex` 0 命中；PRO `A----------.cs:24304/30514` 返回 `(Reclaimed,Bytes)?` | 实证（高） |
| 社区**无** `ImageCapturePostProcessMode` | `grep` 0 命中；PRO `Options.MediaInfo\MediaInfoExtractOptions.cs:307-341` | 实证（高） |
| 社区**无** `ClearRecentTmdbCacheOnStartup` | `grep` 0 命中；PRO `Options.Metadata\MetadataEnhanceOptions.cs:748` + `Plugin.cs:1772/1796` | 实证（高） |
| 社区追更延迟=分钟粒度、PRO=秒且隐藏 | 社区 `Options/GeneralOptions.cs:26-30` + `Common/QueueManager.cs:168`；PRO `Options\GeneralOptions.cs:112`（`[Browsable(false)]`） | 实证（高） |
| 社区**无** `LibraryScopePlayProgress` | `grep` 0 命中；PRO `Options.Experience\ExperienceEnhanceOptions.cs:399-403` | 实证（高） |
| HTTP/strm ISO **已实现** | `Common/IsoMounter.cs:207-372`、`Mod/IsoExtraction.cs:154-173`、`Mod/EnableImageCapture.cs:473-530` | 源码实证（高） |
| 远程 ISO 章节**刻意不补写** | `Mod/IsoExtraction.cs:154-159` 注释（窗口截断，DiscUtils 拒绝构造） | 源码实证（高） |
| 阶段 0 两项**已落地** | `Provider/Douban/DoubanAssist.cs:636`、`Options/MultipleVersionsOptions.cs:25-26`、`Web/Service/MultiVersionService.cs:124` | 源码实证（高） |
| PRO 源码**不能**行级 diff | PRO 逐构建重混淆（Unicode 名 + 控制流平坦化 + 字符串加密）；本报告一律用**语义锚点**（属性名/枚举名/资源键/路由） | 方法论（高） |
| S1 搜索符号集 | PRO 侧在加密段内，**不可还原** | 低（已标注） |
</content>
