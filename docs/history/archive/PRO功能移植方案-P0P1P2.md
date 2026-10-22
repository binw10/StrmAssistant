# StrmAssistant PRO 功能移植 · 分级执行方案（P0/P1/P2）

> **本文件可直接照着开工**。每项都标注：改动文件、证据、风险、验收。
> **基线**：社区版 `3.0.55`（`D:\下载\AI\StrmAssistant-myp015`）｜PRO `v3.0.0.55`（反编译 `_pro_tmp/decompiled`）
> **日期**：2026-10-16
> **升级说明**：本方案对 v6 报告做了**代码级二次核实**，修正了 3 处误判（见 §五）。**以本文件为执行基线。**

---

## 〇、先看结论

| 分级 | 项数 | 性质 | 建议 |
|---|---|---|---|
| **P0** | 0 项 | 无紧急事项 | 已完成项见 §四，无需再动 |
| **P1** | 4 项 | 真缺口 · 低/中风险 · 有明确用户价值 | **本迭代开工** |
| **P2** | 5 项 | 真缺口 · 中/高风险 · 需专项评估 | 排期后逐项立项 |
| **不做** | 若干 | PRO 专有 / 社区已有 / 社区有意移除 | 见 §五 |

**一句话**：真正值得做的只有 **P1 的 4 项**，加起来体量不大、都不碰数据面。P2 里只有 `LibraryScopePlayProgress` 是"大活"，务必单独立项。

---

## 一、P0 · 无（说明）

**没有需要立刻处理的项。** 上一轮 v6 报告中列为 P0 的内容（`ClearEmbeddedMetadata`、`EnableAutomaticIntroScraping`、ISO 跨平台）**均已落地**，无需重复劳动：

| 已落地项 | 落点 | 状态 |
|---|---|---|
| `ClearEmbeddedMetadata` 独立开关 | `Options/MediaInfoExtractOptions.cs`（默认 ON） | ✅ |
| `EnableAutomaticIntroScraping` 独立开关 | `Options/IntroSkipOptions.cs`（默认 OFF，与 PRO 对齐） | ✅ |
| ISO/BDMV 跨平台提取（进程内 UDF + 正片解析） | `Common/UdfTitleResolver.cs` + `Common/IsoMounter.cs` + `Mod/IsoExtraction.cs` | ✅ |
| `EmbyApiVersion` 数值序 bug | `Core/EmbyApiVersion.cs`（现 `V4_10=5000000`） | ✅ 已修 |

> 唯一"待补验"的是 **真碟回归**（见 P1-4）。

---

## 二、P1 · 真缺口，本迭代开工（4 项，按推荐顺序）

### P1-1 · 搜索调优 3 项补齐（`SearchTuningPreferences`）
**价值高、风险低、纯配置 + 轻逻辑。**

| 项 | 内容 |
|---|---|
| PRO 源头 | `ModOptions.SearchTuningPreferences`（6 项多选） |
| 社区现状 | 已有 `EnhanceChineseSearchRestore`、`ExcludeOriginalTitleFromSearch`、`BlockSearchRecommend`（3 项散开关）；**缺 3 项** |
| **缺的第 1 项** | `DigitsAsTmdbId` — 搜索纯数字时直接当 TMDB/Emby ID 精确匹配（代码级 grep 0 命中，确认真缺） |
| **缺的第 2 项** | `ShowMediaInfo` — 搜索结果项上显示媒体信息（0 命中，确认真缺） |
| **缺的第 3 项** | `MixedChineseVariants` — 中文异体字/简繁混合变体互通（现仅有 `TraditionalToSimplified` 逻辑，无变体维度） |
| 改动文件 | `Options/ModOptions.cs`（加 3 个 bool + 三语 resx + Designer）、`Mod/EnhanceChineseSearch.cs`（`CreateSearchTerm` 补 digit 分支）、`Mod/PatchManager.cs`（如需挂新 patch） |
| 风险 | 低。`DigitsAsTmdbId` 只在 Term 改写层加分支，不影响索引 |
| 验收 | 输入纯数字（如 `550`）能精确命中 TMDB id=550 的影片；改名搜索不回归 |

### P1-2 · 元数据刷新「规则」多选化（`MetadataRefreshRule`）
**补齐 PRO 的"刷新规则"维度；社区已有 `EpisodeRefreshOption` 雏形，改造即可。**

| 项 | 内容 |
|---|---|
| PRO 源头 | `MetadataRefreshOptions.MetadataRefreshRuleList`（规则：`NoOverview`/`NoImage`/`NonChineseNameOverview`/`DefaultEpisodeName`/`DefaultSeasonName`/`ReplaceCapturedImage`）+ `MetadataRefreshPreferences` |
| 社区现状 | 有 `EpisodeRefreshOptionList`/`EpisodeRefreshScope`（含 `ReplaceCapturedImage`），**但缺"非中文名简介"等规则维度**，且缺 `MetadataRefreshLibraryList/Scope` 的 PRO 式组织 |
| 改动文件 | `Options/MetadataEnhanceOptions.cs`（`EpisodeRefreshOption` 枚举补 `NonChineseNameOverview` 等）、`Common/LibraryApi.cs:1404` 附近刷新判定、三语 resx |
| 风险 | 低-中。旧配置读兼容（读旧键→映射新枚举） |
| 验收 | 勾选"非中文名简介"后，刷新时只对中文名缺简介的项触发；不勾选行为不变 |

### P1-3 · 豆瓣新版 Token/Client（`DoubanAccessToken` + `DoubanClient`）
**真缺口（代码级确认：社区只有 `DoubanCookie`）。**

| 项 | 内容 |
|---|---|
| PRO 源头 | `MetadataRefreshOptions.DoubanAccessToken` / `DoubanClient` |
| 社区现状 | `Options/MetadataEnhanceOptions.cs:113` 仅 `DoubanCookie` |
| 改动文件 | `Options/MetadataEnhanceOptions.cs`（加 2 项 + 三语 resx + Designer）、`Provider/Douban/DoubanAssist.cs`（请求头/客户端字段透传） |
| 风险 | 低。纯新增请求参数，旧 Cookie 路径保留 |
| 验收 | 填 Token 后豆瓣辅助刮削仍正常；两套凭据互不干扰 |

### P1-4 · ISO 真碟回归（承接 v5 遗留，**需你提供样片**）
**不是新功能，是验证项；但验证不过就得回炉，所以排在 P1。**

| 用例 | 断言 |
|---|---|
| 单 playlist 蓝光 ISO | 选片正确（对比 ffprobe 时长） |
| **多 m2ts / 无缝分支**蓝光 ISO | 选片正确（当前用"playlist 内 PlayItem 时长求和取最长"法，此场景是主要风险） |
| DVD ISO（`VTS_01_1` 存在/缺失） | 分别取 `VTS_01_1.VOB` / 最大 VOB |
| 含 AACS / 损坏 ISO | 静默回退原路径，不中断、不报错 |
| 任意 ISO | `MediaSourceInfo.Path` **未被改写** |
| 生命周期 | 提取后 / 插件卸载无残留挂载点、无残留临时文件 |
| 双端 | x64 + arm64 均能加载，无 `Harmony not implemented` |

> 若"多 m2ts 选片"失败，则升级为 **P2-5** 的重写任务。

---

## 三、P2 · 真缺口，需专项评估（5 项）

### P2-1 · ISO MPLS 深度信息提取（★ Wiki 核心缺口）
**`Mod/UdfTitleResolver.cs` 目前只解析 MPLS 的"时长"用于选片，不解析流表与章节。**

| 目标（对照 Wiki 第 2 条） | 做法 |
|---|---|
| 从 MPLS 提取**音轨语言 / 字幕语言** | 扩展 `ParsePlaylist`：在 PlayItem 之后解析 **STN_table** → `stream_entry`(PID) + `stream_attributes`(language code, ISO-639-2) |
| 从 MPLS 提取**章节** | 解析 `PlayListMark` 段（mark type=1 章节，时间戳 45kHz → ms） |
| 改动文件 | `Common/UdfTitleResolver.cs`（新增 `ParseStn`/`ParseMarks`，约 +150~250 行）、`Mod/IsoExtraction.cs`（把结果注入 ffprobe 结果/日志） |
| 依赖 | **无新依赖**（纯自研字节解析，与现有 MPLS 解析同源） |
| 风险 | 中。需真实 MPLS 样本验证字节偏移（参考 libbluray `mpls_parse.c`） |
| 收益 | 直接命中 Wiki 第 2 条，是"社区 ISO 离 PRO 最近的一步" |

### P2-2 · 双层杜比 Vision P7 检测（Wiki 第 3 条）
| 项 | 内容 |
|---|---|
| 目标 | 探测蓝光原盘的 DV **双层（BL+EL）**，正确显示增强层信息 |
| 做法 | 通过自定义 ffprobe 探测 `dv_bl_signal_compatibility_id` / 增强层流，写入媒体信息 |
| 依赖 | 需自定义 ffprobe（Wiki 已要求用户安装）；无 ffprobe 时降级为不显示 |
| 风险 | 中-高（依赖外部 ffprobe 输出格式） |

### P2-3 · ISO 截图 / 章节图（Wiki 第 5 条）
| 项 | 内容 |
|---|---|
| 目标 | 自定义 ffmpeg 支持 ISO 视频截图与**章节图** |
| 现状 | `CustomEncoderPath` 已有，但 ISO 输入路径分支未打通/未验证 |
| 依赖 | P2-1（章节信息）、P2-2（DV 增强图）、自定义 ffmpeg |
| 风险 | 中。**建议排在 P2-1 之后**，因为章节图依赖 MPLS 章节解析 |

### P2-4 · http / smb 链接内置支持（Wiki 第 4 条）
| 项 | 内容 |
|---|---|
| 目标 | 本地路径之外，直接支持 `http(s)://...iso`、`smb://...iso` |
| 现状 | 仅本地 `.iso`；http/smb 依赖外置 rffmpeg 节点 |
| 做法 | 在 `UdfTitleResolver` 增 `ResolveFromStream`（`UdfReader(Stream)`），为 http 提供可随机读的流包装；`.iso.strm` 命名识别 |
| 风险 | 中-高（网络流 UDF 随机读性能、鉴权） |
| 前置决策 | 走**内置**还是**继续外置 rffmpeg**（PRO 有内置 + 分布式两条路） |

### P2-5 · `LibraryScopePlayProgress`（跨库多版本进度）⚠️ 最高风险
| 项 | 内容 |
|---|---|
| PRO 源头 | `UIFunctionOptions.LibraryScopePlayProgress`：「按媒体库分别记录多版本播放进度（仅剧集）」 |
| 现状 | 代码级 0 命中，确认真缺 |
| 做法 | 需改 `PresentationUniqueKey` **计算链路**，让不同库的多版本产生不同 key |
| 风险 | 🔴 极高。直接改播放进度/收藏/已看状态的键，**可能导致用户数据错乱或丢失** |
| 要求 | **必须单独立项**：先在测试库验证 key 变更无副作用 → 设计回滚方案 → 充分回归。**优先级最低，不急于做** |

---

## 四、已完成（无需再做，仅供核对）

| 项 | 落点 | 状态 |
|---|---|---|
| `ClearEmbeddedMetadata` | `Options/MediaInfoExtractOptions.cs` (默认 ON) | ✅ |
| `EnableAutomaticIntroScraping` | `Options/IntroSkipOptions.cs` (默认 OFF) | ✅ |
| ISO 跨平台提取（UDF + 正片解析 + DVD 识别） | `Common/UdfTitleResolver.cs` 等 | ✅ |
| `FingerprintBlacklistKeywords`（=PRO 的 ExcludeKeywords） | `Options/IntroSkipOptions.cs` + `Common/FingerprintApi.cs:265` | ✅ |
| `EnableAutoMarkWatched`（=PRO 的 MarkPreviousEpisodesPlayed） | `Common/AutoMarkWatched.cs`（标记前集，语义一致） | ✅ |
| `LocalTmdbCache` / `MetadataCacheMode` | `Mod/LocalTmdbCache.cs` + `Options/MetadataEnhanceOptions.cs:291` | ✅ |
| `OptimizeItemOrderPreferences` / `EnforceUserPreferences` | `Mod/*` | ✅ |
| `EmbyApiVersion` 修复 | `Core/EmbyApiVersion.cs` | ✅ |

---

## 五、明确不做（附理由）

| 项 | 理由 |
|---|---|
| `SuppressPluginUpdates` | 社区 **26.3.25 有意移除**（刻意的产品决策） |
| `MemoryOptions` / `SystemInfoItemList` | 社区 `EnableMemoryAutoRestart`+`EnableMemoryCleanup`+`RuntimeStatusList` **更优** |
| 授权 / `Portable.Licensing` | PRO 商业逻辑，社区无意义 |
| `BlurayMounter` / `MonoMod` 直接依赖 | 私有/GPL/技术栈不兼容 |
| `CatchupMinimumDelaySeconds` | 社区 `CatchupMinWaitMinutes` 已等价（仅粒度是分钟） |
| JS 注入套件拆分（PRO 11 端点） | 纯性能优化，非功能缺口，按需再做 |

### ⚠️ 相对 v6 报告的 3 处修正（代码级复核）

| v6 曾列为缺口 | 复核结论 |
|---|---|
| `FingerprintExcludeKeywords` | ❌ **误判**。社区已有 `FingerprintBlacklistKeywords`（`FingerprintApi.cs:265` 消费），仅命名不同 |
| `MarkPreviousEpisodesPlayed` | ❌ **误判**。社区 `EnableAutoMarkWatched` 实现的就是"看完当前集标记前集"（`AutoMarkWatched.cs:99 MarkPreviousEpisodesWatched`），语义一致 |
| `CatchupMinimumDelaySeconds` | ⚠️ **近似重复**。社区 `CatchupMinWaitMinutes` 功能等价，仅时间粒度不同 |

> 修正原因：v6 的集合比对只能发现"属性名不同"，无法区分"改名"与"真缺"；本次已用代码级 grep + 源码阅读逐项验证。

---

## 六、执行路线图

```
【本迭代】
P1-1 搜索调优3项  ─┐
P1-2 刷新规则多选  ├─→ 一轮 dotnet build + 三语 resx 核对 + 部署验证
P1-3 豆瓣Token     │
P1-4 ISO真碟回归   ─┘   （需样片；若"多m2ts选片"失败 → 升级为 P2-1 前置任务）

【下一迭代】
P2-1 MPLS语言/章节 ★  ← 无新依赖、命中Wiki第2条，性价比最高
      ↓（章节信息就绪后）
P2-3 ISO截图/章节图
P2-2 双层杜比P7
P2-4 http/smb（先决策 内置 vs rffmpeg）

【单独立项 · 最后】
P2-5 LibraryScopePlayProgress（高风险，需回滚方案 + 充分回归）
```

### 每阶段通用验收基线
1. `dotnet build` 无错误；ILRepack 合并产物部署到 `StrmAssistantPluginOutputDir`（默认 `D:\Downloads\embyserver\programdata\plugins`）。
2. x64 + arm64 双端启动无 `Harmony not implemented` / `GLIBC not found`；`RuntimeStatusList` 正常。
3. 新增配置后台可见可改、重启持久化、**三语资源无缺**（resx + `.zh.resx` + `.zh-hant.resx` + `Resources.Designer.cs` 四处同步）。
4. 逐项回归：改配置 → 重启 → 验证行为 → 回滚配置 → 验证恢复。
5. patch/Transpiler 改动须在 **4.9.1.90 与 4.10.x** 双版本各跑一次。
6. 工程红线：不引私有/GPL 依赖；不改 `MediaSourceInfo.Path`；枚举本地化用 `DescriptionAttribute`（不用 Emby 4.10 的 `EnumExtensions`）。

---

## 七、工程红线（重申）

1. 技术栈锁 `net6.0`；不引 `BlurayMounter`/`MonoMod`/`Portable.Licensing`。
2. 新依赖须 MIT/BSD/Apache，并同步 `csproj` 的 ILRepack 参数（现合并 7 个 dll）。
3. resx 三语 + Designer 四处同步。
4. 写回类改动「仅补空 + 字段级合并」。
5. 版本适配统一走 `Core/EmbyVersionAdapter`，禁散落硬编码。
