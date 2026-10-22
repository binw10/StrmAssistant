# StrmAssistant 社区版 × PRO 功能对比与优化移植方案（v7）

> **社区版底座**：`D:\下载\AI\StrmAssistant-myp015`（`net6.0`，基线 `mediabrowser.server.core 4.9.1.90`，运行于 Emby 4.10.1.0）
> **参照对象**：PRO `StrmAssistantPro.dll` **新版**反编译源码 `D:\下载\AI\StrmAssistant.Releases\_pro_tmp\decompiled_v2\`（138 个 `.cs`，`netcoreapp6.0`，`3.0.0.55+3abcae8`，构建于 2026-10-15）
> **方法**：以 **decompiled_v2 的真实源码** 为准，逐属性/逐模块做**代码级比对**（不再依赖旧版 v1 的推断），据此重排优先级并清理。
> **日期**：2026-10-17
> **与旧文档关系**：本 v7 **取代 v6**。`-v6.md` 及更早版本仅作过程记录。v7 的关键修正是**把若干"疑似缺口"证伪**（见 §八）。

---

## 〇、结论速览（先看这里）

| 维度 | 结论 |
|---|---|
| **最大变化** | 经 v2 源码核验，v6 里多条"缺口"**实际已在社区版存在**（搜索调优 6 项、`MarkPreviousEpisodesPlayed`、`FingerprintExcludeKeywords`、`DoubanAccessToken` 全齐）。**真缺口收敛为 4 项 P3 小项 + ISO 专项**。 |
| **本轮主角** | 你**主要用 strm 看 ISO**，而 P1-4 已证实：社区 ISO 能力**只覆盖本地 `.iso` 文件**，**完全不覆盖 strm/HTTP 形态的 ISO**——即你最高频的使用场景。→ **"B（strm/HTTP ISO 支持）"从 P2 提前，升为最高优先级**。 |
| **B 的技术路线** | PRO 的做法已从 `strings.txt` 完全还原：自定义 ffprobe 以 **`bluray:"{0}"`** 直读（`--enable-libbluray`），并配套 `ExtractBlurayDisc` 处理 `.iso.strm` / `PLAYLIST` / `CLIPINF` / `STREAM` / `.ssif`。社区只需**命中条件放开 + 探测输入改写**，无需引入 PRO 私有库。 |
| **可清理项** | 一次性列出**不移植/不值得移植**清单（授权、`BdInfoExaminer` 私有港、`MemoryOptions`、`SuppressPluginUpdates`、PRO 的"多选偏好"形态外壳等），见 §六。 |
| **优化后策略** | **两层**：**L-A 用户价值层**（strm/HTTP ISO → 本地 ISO 深挖 → 元数据小项）→ **L-B 可选对齐层**（配置形态收敛、重型能力各自立项）。 |

---

## 一、基石差异（决定"能不能直接搬代码"）

| 项 | PRO (v2) | 社区版 | 结论 |
|---|---|---|---|
| 目标框架 | `netcoreapp6.0` | `net6.0` | 等价 |
| 补丁技术 | **MonoMod** + 混合 Harmony | **Lib.Harmony 2.4.2** | ⛔ IL patch 不可搬，只能按行为重写 |
| ISO 机制 | **Harmony Transpiler** 改 `IFileSystem.OpenRead` → 进程内 `UdfReader`（让 Emby 自己读 UDF）+ 内置 `BdInfoExaminer`（私有 BDInfo 港） | **Harmony Prefix** 只改 ffprobe 的 `inputPath` + 自研 MPLS 选片 + 挂载/自定义 ffprobe | 语义可对齐，实现不可搬 |
| 自定义 ffprobe | **必需**（`bluray:`/`libbluray`） | 已有 `CustomProbePath`+`FfmpegPathOverride` | 社区**基建已就位**，缺"用到 HTTP ISO 上" |
| 授权 | `Portable.Licensing` + 功能门控 | 无 | 不移植 |
| 配置页 | 分组（General/Network/Mod/Experience/IntroSkip×3/MediaInfo/Metadata×4/Dashboard） | 扁平 11 类 | 形态差异，非功能缺口 |

**关键**：PRO v2 的 `strings.txt` 已把 ISO 逻辑**明文暴露**（未混淆），这轮实现可直接对齐其**行为规格**：

```
[174248] ExtractBlurayDisc          ← 专类名
[174270] .iso.strm                  ← 识别 strm 形态 ISO
[174308] OpenReadV                  ← 劫持 IFileSystem.OpenRead
[174428..174558] PLAYLIST *.mpls / CLIPINF *.clpi / STREAM *.m2ts / *.ssif
[174996] $1$2,bluray                ← 正则替换：把输入串尾追 ,bluray
[175184] bluray:"{0}"               ← ffprobe 输入协议
[175216] http://  [175238] https://@  ← 支持 http(s) 输入
[2419] --enable-libbluray*          ← 能力检测
[2426] Custom ffprobe supports bluray input protocol
```

---

## 二、代码级缺口矩阵（基于 decompiled_v2 实测）

> 判定口径：**在 v2 源码里逐属性 grep** → 再在社区版逐属性 grep。仅"PRO 有、社区无"才计为缺口。

### 🔴 A 类：真功能性缺口（**经二次代码核验后进一步收敛**）

| # | PRO 属性（v2 文件:行） | 语义 | 社区现状（代码实证） | 优先级 |
|---|---|---|---|---|
| A1 | `ImageCapturePostProcessMode`（`MediaInfo/MediaInfoExtractOptions.cs:309,323`） | 截图后处理（含 DV 增强） | 无 | **P3** |
| A2 | `ClearRecentTmdbCacheOnStartup`（`Metadata/MetadataEnhanceOptions.cs:748`） | 启动清 TMDB 最近缓存 | 无（社区有 `LocalTmdbCache`，但无"启动清理"动作） | **P3（小）** |
| A3 | `LibraryScopePlayProgress`（`Experience/ExperienceEnhanceOptions.cs:403`） | 按库分别记多版本进度（仅剧集） | 无 | **P3（高风险）** |
| A4 | `CatchupMinimumDelaySeconds`（`GeneralOptions.cs:112`） | 追更最小延迟（秒） | 有 `CatchupMinWaitMinutes`（分钟粒度，**功能等价**，仅刻度不同） | **P3（可不做）** |

> ⚠️ **本轮重大修正**：v6 列的两个"P1 缺口"经代码核验**均已在社区版实现**（详见 §八）：
> - `MarkPreviousEpisodesPlayed` ↔ 社区 `EnableAutoMarkWatched` + `AutoMarkWatched.MarkPreviousEpisodesWatched()`（`Common/AutoMarkWatched.cs:99`）—— PRO 描述"Mark previous in-progress episodes as played after finishing the current one" 与社区描述"mark previous episodes as watched when current episode reaches the progress threshold" **语义等价**。
> - `FingerprintExcludeKeywords` ↔ 社区 `FingerprintBlacklistKeywords`（`Options/IntroSkipOptions.cs:99`）—— **同名双开关均已存在**（`FingerprintBlacklistShows` 剧名 + `FingerprintBlacklistKeywords` 关键字）。
>
> **净结论：真功能性缺口仅剩 4 项，且全部 P3（低优先）。** 本轮真正的高价值目标只有一个 —— **B（strm/HTTP ISO）**。

### 🟢 B 类：**已核实社区版已实现**（v6 曾误判为缺口 → 本版**删除**）

| PRO 属性 | 社区对应 | 核验 |
|---|---|---|
| `SearchTuningPreferences`（多选 6 项） | `EnhanceChineseSearch`+`ExcludeOriginalTitleFromSearch`+`DigitsAsTmdbId`+`MixedChineseVariants`+`ShowMediaInfo`+`SearchScope` | ✅ **6 项全有**（`ModOptions.cs:19-85`，P1-1 已交付） |
| `DigitsAsTmdbId` / `ShowMediaInfo` / `MixedChineseVariants` | 同名 bool | ✅ 已在 `ModOptions.cs:69/75/81` |
| `DoubanAccessToken` | 同名 string | ✅ 已在 `MetadataEnhanceOptions.cs`（P1-3 已交付） |
| `DefaultSeasonName` / `DefaultImageRefresh` / `FullImageRefresh` | 枚举值 | ✅ 已交付（P1-2） |
| `MarkPreviousEpisodesPlayed` + `...Threshold`（多选偏好） | `EnableAutoMarkWatched` + `AutoMarkWatchedThreshold` + `AutoMarkWatchedExcludeFavorites` | ✅ **语义等价**（`ExperienceEnhanceOptions.cs:88-124` + `Common/AutoMarkWatched.cs:99`） |
| `FingerprintExcludeKeywords` | `FingerprintBlacklistKeywords` | ✅ **已实现**（`Options/IntroSkipOptions.cs:99`，与 `FingerprintBlacklistShows` 并存） |
| `CatchupMinimumDelaySeconds` | `CatchupMinWaitMinutes` | 🟡 **功能等价**（仅秒/分钟刻度差） |

> **结论**：v6 的 B1/B9"搜索调优归一"整块**不成立**——社区版不仅"有"，而且是**独立开关形态**（比 PRO 的单串多选**对用户更直观**）。**无需改动**（想统一 UI 形态属"可选美化"，非功能需求）。

### 🟡 C 类：PRO 独有形态（功能在、UI 分组不同，**低价值不追**）

`MetadataRefreshPreferences+RuleList`、`PersonRefreshOptionList+Preferences`、`BeautifyMultiVersion+DisplayPreference+ReorderPreference`、`MetadataCacheOptionList`、`IntroSkip*Options` 嵌套子对象、`MultipleVersionsOptions` 嵌套对象、`Is*PluginSupported` 命名 —— 均为 **"多选偏好+规则"范式**，社区以 bool/单值枚举实现，**语义等价**。追形态收益低、回归面大。

### ⛔ D 类：确定不移植（清理清单见 §六）

`Portable.Licensing` 全套、`MemoryOptions`/`SystemInfoItemList`、`SuppressPluginUpdates`、`BdInfoExaminer`/`BlurayMounter` 私有港、`LastProxyCheckSucceeded`、`ShowMetadataExtraOptions`。

---

## 三、ISO/BDMV 专项（本轮重点，按 Wiki 6 条验收）

| Wiki 能力 | PRO | 社区现状（代码实证） | 状态 |
|---|---|---|---|
| 1. 提取 ISO/BDMV 媒体信息、记录进度 | ✅ | ✅ 本地 `.iso` 已通（`IsoExtraction` patch `MediaProbeManager.GetMediaInfoInternal` 的 `inputPath`） | 🟢 本地通 |
| 2. 从 **MPLS** 提取音轨/字幕语言、章节 | ✅ | ⚠️ `UdfTitleResolver` 只选正片，**未提取语言/章节** | 🔴 缺口 |
| 3. 检测**双层杜比 P7** | ✅ | ❌ 无 | 🔴 缺口 |
| 4. 支持 本地/http/**smb** | ✅ | ⚠️ **仅本地**；http/smb **不命中**（P1-4 已证） | 🔴 **缺口（你的主场景）** |
| 5. 自定义 ffmpeg 的 ISO 截图/章节图 | ✅ | ⚠️ `CustomEncoderPath` 就位，ISO 分支未验证 | 🟡 部分 |
| 6. DVD ISO + 自动识别 | ✅ | ✅ `ResolveDvd`+`IsBluray` 判定 | 🟢 待真碟 |

### 🎯 P1-4 的核心发现（决定本轮排序）

样本 `杜比视界测试：双层 FEL.iso`（条目 253）：
- 条目形态 = `.iso.strm` → `MediaSource.Path` = **HTTP URL**（115 CDN），`Protocol=Http`、`Container=strm`。
- **从未被成功探测**：`Container=None`、`RunTimeTicks=None`、无媒体流。
- `IsoMounter.TryNormalizeIsoPath` 有两道**硬门槛**直接拦死 HTTP 形态：
  ```csharp
  if (candidate.Contains("://")) return false;   // ← HTTP URL 出局
  if (!File.Exists(candidate)) return false;      // ← 还要求本地文件存在
  ```
- **但内置 ffprobe 其实能读 ISO**（实测：`format=mpegts`，双 HEVC `codec_tag=HDMV`，4K Main10 + 1080p Main10 = **DV P7 双层 FEL**）。

> **一句话根因**：社区 ISO 能力的设计边界是"**本地文件**"，而你最常用的"**strm 代理 ISO**"正好落在边界之外。

### B 的实现路线（**对齐 PRO 已验证规格，不引私有库**）

```
[识别]  条目 MediaSource.Protocol==Http && path/file_name 以 .iso 结尾
          （strm 直链场景：真实 URL 来自 StrmDirectRedirect 已解析的 302 目标）
   ↓
[改写]  把 ffprobe 输入从  "<url>"  改为  bluray:"<url>"
          （PRO 用正则 "$1$2,bluray" 追加，社区在 Prefix 里等价改写 inputPath）
   ↓
[探测]  CustomProbePath 指向带 --enable-libbluray 的 ffprobe
          （社区 FfmpegPathOverride.SelfCheck 已会检测 "bluray" 关键字）
   ↓
[兜底]  若自定义 ffprobe 不支持 libbluray → 保持原路径放行，不影响常规条目
```

**改动面（全部在社区版内，零新依赖）**：
1. `Common/IsoMounter.cs`：新增 `TryNormalizeHttpIso(url,out url)` —— 允许 `://` + `.iso` 结尾（去掉 `File.Exists` 硬要求，改为"strm 场景跳过本地校验"）。
2. `Mod/IsoExtraction.cs`：`GetMediaInfoInternalPrefix` 增加 HTTP 分支 —— 命中则把 `inputPath` 包成 `bluray:"{0}"`。
3. `Common/FfmpegPathOverride.cs`：把 SelfCheck 结果**缓存为能力位**（`SupportsBluray`），供 `IsoExtraction` 决策；不支持时**不改写**（安全回退）。
4. `Options/MediaInfoExtractOptions.cs`：`EnableIsoExtraction` 的说明文案补充"支持 strm/http ISO（需自定义 ffprobe 带 libbluray）"；三语 resx 同步。

> **风险**：中。仅改**探测输入串**，仍**不触碰 `MediaSourceInfo.Path`**（守住 §四 红线 6），播放路径与条目身份不受影响；能力不足时自动回退。

---

## 四、移植工程红线（沿用，全部仍有效）

1. 技术栈锁 `net6.0`，**不引** PRO 私有强依赖（`BlurayMounter`、`MonoMod`、`Portable.Licensing`）。
2. 新增依赖须 MIT/BSD/Apache，并同步 `csproj` 的 **ILRepack 合并参数**（现合并 7 个 dll）。
3. `Resources.resx` + `.zh.resx` + `.zh-hant.resx` + `Resources.Designer.cs` **四处必须同步**。
4. 所有写回一律「**仅补空 + 字段级合并**」，禁覆盖用户数据。
5. 枚举本地化不依赖 4.10 的 `EnumExtensions.GetDescription`，用基类匹配的 `DescriptionAttribute`。
6. **`MediaSourceInfo.Path` 不可改写**（保护播放路径与条目身份）。
7. 版本适配统一走 `Core/EmbyVersionAdapter`。

---

## 五、优化后的迭代顺序（按**你的实际用法**重排）

> 排序原则：**"你主要用 strm 看 ISO"** ⇒ strm/HTTP ISO 的价值 **>** 本地 ISO 深挖 **>** 元数据小项。故原 P2-1（MPLS 语言/章节）**让位**给 B。

```
【第 1 步 · 最高优先】B — strm/HTTP ISO 支持（对齐 PRO bluray 协议）
   ├─ 命中条件放开（IsoMounter 增 HTTP 分支）
   ├─ 探测输入改写 bluray:"{0}"（IsoExtraction）
   ├─ 自定义 ffprobe 能力位（FfmpegPathOverride）
   └─ 验收：样本 253 重新探测后 Container/RunTimeTicks/流 不再为 None
        ↓
【第 2 步】本地 ISO 深挖 — MPLS 语言/章节提取（Wiki#2，原 P2-1）
   └─ 纯自研、无新依赖；扩展 UdfTitleResolver.ParsePlaylist 解析 STN_table + PlayListMark
        ↓
【第 3 步】双层杜比 P7 标注（Wiki#3）
   └─ 依赖自定义 ffprobe 输出（与 B 共用基建，B 做完顺手做）
        ↓
【第 4 步】ISO 截图/章节图验证（Wiki#5）
   └─ 复用 EnableImageCapture + CustomEncoderPath，补 ISO 输入分支
        ↓
【第 5 步 · 可选小项批次】（低风险，一轮构建）
   └─ A2 启动清 TMDB 缓存 + A4 追更秒粒度（若你需要）
        ↓
【第 6 步 · 各自立项】A3 跨库多版本进度（高风险）→ A1 截图后处理 → smb ISO
```

> **说明**：v6 排在第 1 梯队的 `MarkPreviousEpisodesPlayed`、`FingerprintExcludeKeywords` **已实现**，故整体提前一个身位 —— 本轮**只需攻克 ISO 专项**，其余均为"有余力再做"。

**验收基线（通用）**
1. `dotnet build` 0 错误；ILRepack 产物部署到两个 plugins 目录，MD5 一致。
2. Emby 重启日志：插件加载、补丁数正常、Healthy、0 error。
3. 新增配置可见可改、重启持久化、三语无缺。
4. patch 类改动在 **4.9.1.90 与 4.10.x** 双版本各跑一次。

---

## 六、清理清单（**不移植 / 不值得移植**，一轮剔除）

| 项 | 处理 | 理由（代码依据） |
|---|---|---|
| `Portable.Licensing` 全套 | ⛔ 不移植 | PRO 商业授权，社区无意义 |
| `BdInfoExaminer` / `BlurayMounter` 私有港 | ⛔ 不移植 | 私有/GPL 依赖；社区自研 `UdfTitleResolver` 已覆盖选片，语言/章节按 §五-2 自研补 |
| `MemoryOptions` / `MemoryUsageLimitMB` / `SystemInfoItemList` | ⛔ 不移植 | 社区 `EnableMemoryAutoRestart`+`EnableMemoryCleanup`+`RuntimeStatusList` **更优** |
| `SuppressPluginUpdates` | ⛔ 不移植 | 社区 26.3.25 **主动移除**（刻意决策） |
| `LastProxyCheckSucceeded` | ⛔ 不移植 | PRO 内部字段，社区 `ProxyServerStatus` 已覆盖 |
| `ShowMetadataExtraOptions` | ⛔ 不移植 | PRO 的 UI 折叠开关，无功能意义 |
| B 类"多选偏好范式"外壳（`*Preferences`/`*RuleList`/嵌套子对象） | ⛔ 不追 | 社区 bool/单值枚举**语义等价**，追形态收益低、回归面大 |
| `MetadataRefreshPreferences+RuleList` 整体改造 | ⛔ 不追 | 语义已由社区 `EpisodeRefreshOptionList` 覆盖（P1-2 刚补 `DefaultSeasonName`） |

> **净结果**：真待办从 v6 的"~24 项"**收敛为 ~8 项**（A1–A6 + B），其余为已实现或形态差异。

---

## 七、证据与可信度

| 结论 | 证据 | 等级 |
|---|---|---|
| PRO 新版 138 个 .cs、3.0.0.55+3abcae8 | `_pro_tmp/decompile_report.md` | 一手 |
| `MarkPreviousEpisodesPlayed` 等在 PRO v2 存在 | `decompiled_v2/StrmAssistant/Options/Experience/ExperienceEnhanceOptions.cs:160` 等（逐属性 grep） | 源码实证（高） |
| 社区版**已有**搜索调优 6 项 | `StrmAssistant/Options/ModOptions.cs:19-85` | 源码实证（高） |
| PRO ISO 走 `bluray:"{0}"`+`--enable-libbluray`+`ExtractBlurayDisc` | `_pro_tmp/strings.txt:2045-2085,2419,2426,2437` | 反编译实证（高） |
| 社区 ISO **不覆盖 HTTP/strm** | `Common/IsoMounter.cs:89-93`（`://`/`File.Exists` 双门槛）+ P1-4 诊断 | 源码+实测（高） |
| 内置 ffprobe 能读 ISO（含 DV P7 双层） | P1-4 §二·补实测 | 实测（高） |

**弱点**：B 类"形态等价"为语义推断（已在 §二 C 类标注）；`LibraryScopePlayProgress` 改动面为高风险推断（需专项验证）。

---

## 八、与 v6 的差异说明（**修正记录**）

| v6 判断 | v7 修正 |
|---|---|
| B1"搜索调优缺 3 项"（`DigitsAsTmdbId`/`ShowMediaInfo`/`MixedChineseVariants`） | ❌ **证伪**：社区版 `ModOptions.cs:69/75/81` **全部已实现**（P1-1 已交付） |
| A2"`MarkPreviousEpisodesPlayed` 与社区不等价"（列为 P1） | ❌ **证伪**：社区 `EnableAutoMarkWatched` + `AutoMarkWatched.cs:99 MarkPreviousEpisodesWatched()` **语义等价**，非缺口 |
| A6"`FingerprintExcludeKeywords` 缺口"（列为 P1 小） | ❌ **证伪**：社区 `IntroSkipOptions.cs:99 FingerprintBlacklistKeywords` **已实现** |
| B2/B3"刷新偏好多选化"列为缺口 | ⚠️ 降级为 C 类（形态差异，非功能缺口） |
| A5 豆瓣 Token 列为 P2 缺口 | ✅ **已交付**（P1-3） |
| 排序：L2-1 MPLS 语言/章节为"最高性价比" | 🔄 **让位**：B（strm/HTTP ISO）升为第 1 步，MPLS 降为第 2 步 |
| 真缺口 ~24 项 | ✅ 收敛为 **4 项 P3（A1–A4）+ ISO 专项（B/MPLS/DV）** |
