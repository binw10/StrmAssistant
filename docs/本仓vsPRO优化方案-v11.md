# 本仓 vs PRO 优化方案（v11）

> **基准（PRO 侧）**：`D:\Downloads\ai\StrmAssistant.Releases\StrmAssistantPro.decompiled`（`StrmAssistantPro` **3.0.0.55**，ConfuserEx 混淆：标识符重命名 + 控制流平坦化 + 字符串加密）
> **对照方（社区侧）**：`D:\Downloads\ai\StrmAssistant\StrmAssistant\`（分支 `myp015`，`net6.0` + Lib.Harmony，`StrmAssistantPlus` 3.0.0.55）
> **方法**：PRO 侧 **139 个 `.cs` 全量盘点**（文件级核对表 100% 覆盖）+ 本仓逐行核对 + **Emby 实机 API 实测**（见 `docs/豆瓣功能全量实测-2026-10-16.md`）
> **日期**：2026-10-16　**状态**：与 `PRO功能对比与移植方案-v10-最终版.md` 并存；**v10 的 R1~R6 仍然有效**，本版**新增豆瓣侧 D1~D5**（v10 未覆盖），并修正 v10 的「对齐度 95%」结论。
> **一句话**：非豆瓣面确实高度对齐（v10 的 6 项都属锦上添花）；但**豆瓣面存在 5 处结构性缺口**，其中 **D1 直接造成用户可见的「图标」缺失**（实机已复现）。

---

## 〇、一页看懂

| 层面 | v10 结论 | v11 修正 |
|---|---|---|
| 非豆瓣功能对齐度 | ~95%，剩 6 项低优先 | **维持**（R1~R6 全部有效，见 §二） |
| **豆瓣功能对齐度** | 未单列（隐含「已对齐」） | **存在 5 处结构性缺口（D1~D5）**，其中 D1/D2 应立即处理 |
| 用户可见影响 | — | **D1 导致条目页无「豆瓣」链接图标**（实机：item 191 `ExternalUrls` 仅 IMDb/TMDB/Trakt） |
| 最高优先动作 | — | **D1 移植 `DoubanExternalId`（≈30 行）** + **D2 快照漏记修复（≈3 行）** |

### 优先级速览

| 编号 | 项 | 优先级 | 工作量 | 风险 | 用户可见 |
|---|---|---|---|---|---|
| **D1** | `DoubanExternalId` 外部链接图标 | **P1** | 极小（新文件 ~30 行） | 极低 | ✅ 直接可见 |
| **D2** | 评分快照缓存命中漏记 | **P1** | 极小（~3 行） | 极低 | ✅ 徽章可靠性 |
| **D3** | 季级豆瓣映射（季列表接口 + 映射表） | **P2** | 中（~200 行） | 低 | ✅ 季条目评分/徽章 |
| **D4** | `TriggerPopulateDoubanIdTask`（扫描后自动补 ID） | **P2** | 小（~80 行） | 低 | 间接 |
| **D5** | 豆瓣数据模型 DTO 化 + 限制级/黑名单/可播放性 | **P3** | 大（重构） | 中 | 间接 |
| R1~R6 | 见 §二（沿用 v10） | P2~P4 | 小~大 | 低~高 | 部分 |

---

## 一、豆瓣侧缺口（本轮新增 · 核心）

### D1 · 豆瓣外部链接图标缺失 —— 移植 `DoubanExternalId`　【P1 · 极低风险】

**实测证据（本仓）**

```
GET /emby/Users/{uid}/Items/191?Fields=ExternalUrls,ProviderIds
ProviderIds: {'Imdb':'tt38984207','Tmdb':'1481955','Douban':'37193250'}
ExternalUrls: IMDb / TheMovieDb / Trakt     ← 3 条，无 Douban
```

**PRO 实现**

| 项 | 位置 |
|---|---|
| 类声明 | `StrmAssistant/Provider/DoubanExternalId.cs:11` → `public class DoubanExternalId : IExternalId, IHasWebsite` |
| `Name` / `Key` | 均返回 `StaticName`（`DoubanExternalId.cs:15/17`） |
| `UrlFormatString` | `DoubanExternalId.cs:19`（字符串加密，语义 = `https://movie.douban.com/subject/{0}/`） |
| `Website` | `DoubanExternalId.cs:23`（语义 = `https://movie.douban.com`） |
| `Supports` | `DoubanExternalId.cs:25-49` → **Movie \| Series \| Season** 均为 true |

**本仓现状**

- `grep -rn "IExternalId" StrmAssistant/` → **仅 1 命中**：`Provider/MovieDbEpisodeGroupExternalId.cs:8`。
- **无 `DoubanExternalId`** → Emby 无法为 `ProviderIds["Douban"]` 生成外部链接，条目页/详情页**不显示豆瓣图标**。

**落点建议**

1. 新增 `StrmAssistant/Provider/DoubanExternalId.cs`：

```csharp
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace StrmAssistant.Provider
{
    public class DoubanExternalId : IExternalId, IHasWebsite
    {
        public string Name => StaticName;
        public string Key => StaticName;
        public string UrlFormatString => "https://movie.douban.com/subject/{0}/";
        public string Website => "https://movie.douban.com";

        public bool Supports(IHasProviderIds item) => item is Movie || item is Series || item is Season;

        public static string StaticName => "Douban";
    }
}
```

2. **无需注册**：Emby 对 `IExternalId` 走程序集扫描自动发现（PRO 亦然）。
3. `StaticName` 必须与 `DoubanAssist` 写入 `ProviderIds` 用的键**完全一致**——本仓该键由 `DoubanProviderId` 常量承载（`Provider/Douban/DoubanAssist.cs` 内），落盘值实测为 `"Douban"`，请直接引用该常量而非硬编码。

**验收**

- `GET /Items/{id}?Fields=ExternalUrls` 出现 `Douban` 条目，URL 形如 `https://movie.douban.com/subject/37193250/`；
- Emby 网页条目页外部链接区出现豆瓣入口，点击可跳转；
- **不改任何数据**，仅新增 Provider。

---

### D2 · 评分快照「缓存命中漏记」—— 徽章数据源健壮性　【P1 · 极低风险】

**本仓实现（问题点）**

```csharp
// Provider/Douban/DoubanAssist.cs:972-1025
private async Task<DoubanSubject> FetchSubjectAsync(string subjectId, ...)
{
    var key = "subject:" + subjectId;
    var cached = GetCache<DoubanSubject>(key);
    if (cached != null) return cached;        // ← :976-977 命中缓存直接返回，不补记快照
    ...
    if (subject.Rating.HasValue)
        RecordRatingSnapshot(subjectId, subject.Rating.Value, subject.Votes);   // :1021 仅新取时记录
}
```

- 评分快照（`strmassistant_douban_ratings.json`）是**徽章唯一数据源**：`GetRatings()`（`:274`）按条目 `ProviderIds["Douban"]` 查快照，查不到即无徽章。
- 快照文件与缓存文件（`strmassistant_douban_cache.json`）**生命周期完全独立**：
  - 若快照文件被删/首次生成晚于缓存建立（升级、清理、异常），**缓存命中 → 永不补记**；
  - `BuildCacheAsync`（`:432-441`）对「已有豆瓣 ID 且缓存未过期」的条目**直接跳过**，重跑任务也无法回填；
  - 缓存有效期 7 天（`CacheDuration`，`:143`）→ 最长 7 天内徽章一直缺失。
- **实测佐证**：4 个 subject 全部在缓存中，但快照只有 3 条。缺的那条（`37069873`）经源站验证为豆瓣「暂无评分」，**属正确行为**；但机制上的漏记路径确实存在（本机恰好未触发）。

**修法（最小改动）**

```csharp
var cached = GetCache<DoubanSubject>(key);
if (cached != null)
{
    if (cached.Rating.HasValue)
        RecordRatingSnapshot(subjectId, cached.Rating.Value, cached.Votes);   // 命中缓存也补记
    return cached;
}
```

（`RecordRatingSnapshot` 自身已有「值未变则 return」的幂等判断，`:320-325`，重复调用无副作用。）

**可选加固**：`BuildCacheAsync` 对「缓存未过期」的条目，仍从缓存补记一次快照（覆盖「快照丢失」场景）。

**验收**：删除 `strmassistant_douban_ratings.json` → 重启 → 触发一次条目刷新/缓存任务 → 快照文件被重建，徽章恢复；日志无新增 400/风控。

---

### D3 · 季级豆瓣映射缺失 —— 季条目几乎全部匹配失败　【P2 · 低风险】

**实测证据（本仓）**

```
DoubanAssist - No douban match for: 第 1 季        ×≈20   ← 季条目全部落空
DoubanAssist - Matched 夫妻的博弈 -> subject 37069873    ← 仅剧集主条目命中
```

**本仓实现**：季匹配**只有标题搜索一条路**（`Provider/Douban/DoubanAssist.cs:798-835`）：
`{剧名} 第N季` → 中文数字形式 `{剧名} 第一季` → 从 suggest 结果里挑「标题含『季』」的条目。剧名若有别名/标点差异即失败。

**PRO 实现**

| 能力 | 位置 |
|---|---|
| 季列表接口 DTO | `StrmAssistant/Dto/Douban/DoubanSeasonsResponse.cs`（`id:148`、`title:176`、`is_released:190`、`release_date:260`、`rating:64`…） |
| 仅取已上映季 | `A----------/A----------.cs:81729` → `Where(x => x.is_released)` |
| 季映射表 | `ScheduledTask/BuildDoubanCacheTask.cs:4219 UpdateSeasonMap(...)`；内部类 `A_0098_0096_008B_0099_009A_0097_008B_008C_008A_0086`（`BuildDoubanCacheTask.cs:166-235`，`SortedDictionary<int,string>`） |
| 季→集 映射工具 | `A----------/A----------.cs:76875`（`Season` 重载）、`:76945`（`Episode` 重载） |
| 缓存任务内评分更新 | `ScheduledTask/BuildDoubanCacheTask.cs:4154 UpdateDoubanRating(...)` |

**差距**：本仓**无季列表接口、无季映射表**，季条目拿不到豆瓣评分/演员/海报。

**落点建议**

1. 新增 `Dto` 层：`DoubanSeasonsResponse`（只需 `id` / `title` / `year` / `is_released` / `release_date` 五个字段起步）；
2. `DoubanAssist` 新增 `ResolveSeasonSubjectIdAsync(Series, int seasonIndex)`：拉取剧集主条目 → 取季列表 → 过滤 `is_released` → 按**年份 + 序位**匹配（比标题匹配稳）；
3. `FindSubjectAsync` 的 `seasonNumber.HasValue` 分支改为：**先走季列表映射**，失败再回退现有标题搜索（保持向后兼容）；
4. 季映射结果落盘（复用现有 `_cache`，key 形如 `season:{seriesDoubanId}:{n}`）。

**验收**：同一剧集的多季条目各自匹配到正确季 subject；日志 `No douban match for: 第 N 季` 显著下降；季条目出现评分与徽章。

---

### D4 · 库扫描后自动补豆瓣 ID —— 移植 `TriggerPopulateDoubanIdTask`　【P2 · 低风险】

**PRO 实现**

| 项 | 位置 |
|---|---|
| 类 | `StrmAssistant/ScheduledTask/TriggerPopulateDoubanIdTask.cs:12` → `: ILibraryPostScanTask` |
| 入口 | `TriggerPopulateDoubanIdTask.cs:16 Run(IProgress<double>, CancellationToken)` → 调用豆瓣服务的「ID 填充」方法 |
| 机制 | Emby 对 `ILibraryPostScanTask` **自动扫描并执行**，无需注册 |

**本仓现状**

- `grep ILibraryPostScanTask` → **仅 1 命中**：`ScheduledTask/MergeMultiVersionTask.cs:21`（`TriggerMergeMovieTask`）。
- 豆瓣 ID 填充**只挂在刷新事件**上：`Plugin.cs:412-425`（`OnRefreshCompleted` → `DoubanAssist.ApplyAsync`）。
- 后果：新入库 / 批量扫描 / 手工扫描库之后，**不会自动补豆瓣 ID**，必须等条目逐个刷新。

**落点建议**

1. 新增 `StrmAssistant/ScheduledTask/TriggerPopulateDoubanIdTask.cs`：`: ILibraryPostScanTask`；
2. `Run()` 内遍历本次扫描的库条目（或复用 `BuildDoubanCacheTask` 的取条目逻辑），对「有 IMDb/TMDB 但无 `ProviderIds["Douban"]`」的条目调用 `DoubanAssist` 的解析方法补 ID；
3. **必须加开关与限流**：PRO 该任务受 `IsLicensed` 门控；本仓建议挂在 `DoubanAssistOptions` 下（如 `PopulateDoubanIdOnScan`，默认 **false**），并复用现有 `RequestInterval`（2s）与断路器，避免扫描后瞬时打爆豆瓣。

**验收**：新入库一个中文条目 → 扫描库 → 无需手工刷新即出现 `ProviderIds.Douban`；关闭开关后行为回到现状。

---

### D5 · 豆瓣数据模型 DTO 化 + 限制级/黑名单/可播放性　【P3 · 中风险 · 重构】

**PRO 实现**

| 能力 | 位置 |
|---|---|
| 10 个 DTO | `StrmAssistant/Dto/Douban/`：`Attributes` / `Celebrity` / `DoubanAbstractResponse` / `DoubanCelebrityResponse` / `DoubanDetailResponse` / `DoubanErrorResponse` / `DoubanInfoResponse` / `DoubanSeasonsResponse` / `Picture` / `RatingDetail` / `RatingInfo` |
| 限制级 | `Dto/Douban/DoubanDetailResponse.cs:458 is_restrictive` |
| 黑名单 | `DoubanAbstractResponse.Subject.blacklisted:97`、`DoubanDetailResponse.in_blacklist:430` |
| 可播放性 | `DoubanAbstractResponse.Subject.playable:265`、`DoubanDetailResponse.has_linewatch:248` |
| 演员/角色 | `Dto/Douban/Celebrity.cs:93 character`、`:51 roles`、`:135 latin_name` |
| 导演/主演 | `Dto/Douban/Attributes.cs:121 director`、`:135 cast` |
| 双通道评分 | 搜索摘要 `Subject.star:83` + 详情 `rating.value/star_count`（`RatingDetail.cs:66/52`） |
| 演员→豆瓣 ID 映射 | `A----------/A----------.cs:76737` 工具类（`PersonInfo` 双向映射 `:76960/:76972`） |
| 名人详情接口 | `A----------/A----------.cs:82155/82279/82435`（`DoubanCelebrityResponse`） |

**本仓现状**

- 豆瓣逻辑**全部内联**在 `Provider/Douban/DoubanAssist.cs`（1325 行），用 **正则解析 HTML**（`RatingRegex` / `VotesRegex` / `CelebrityNameRegex` 等，`:218-236`）。
- 无 DTO 层；无 `is_restrictive` / `blacklisted` / `has_linewatch` / `playable` 字段；无独立名人接口（演员中文名从详情页 HTML 正则提取，`:233-236`）。
- **优点**：零依赖、无开放平台配额压力；**缺点**：正则对豆瓣改版脆弱，且拿不到结构化字段。

**落点建议（渐进式，不一次性重构）**

1. **先补字段**：`DoubanSubject` 增加 `IsRestrictive` / `Blacklisted` / `HasLinewatch`，从现有 HTML 里尽力解析（拿不到就留 null）；
2. **限制级接入 Emby**：`IsRestrictive` → `item.OfficialRating` 映射策略（需产品决策，可后置）；
3. **再 DTO 化**：把 `V2 API`（`DoubanAccessToken`）路径抽成 `Dto/Douban/*` 结构，HTML 路径保留为正则兜底——**两条通道并存**（PRO 亦是双通道）；
4. **不做**：PRO 的 `Portable.Licensing` 门控、混淆段内的私有端点。

**验收**：`DoubanAccessToken` 配置后走 DTO 解析；未配置时正则兜底行为与现状**完全一致**（回归集比对）。

---

## 二、非豆瓣缺口（沿用 v10 的 R1~R6，状态不变）

> v10 的结论经本轮复核**全部成立**，此处仅列状态，详细 PRO 锚点/落点见 v10 正文。

| 编号 | 项 | 优先级 | 本仓现状 | 本轮复核 |
|---|---|---|---|---|
| **R1** | 筛选菜单 `HasDoubanId` / `HasImdbId` | P2 | ABSENT（PRO `Web/Api/GetFilterMenuHtml.cs:10` + `ScriptInjectService` 覆写模板） | ✅ 仍缺 |
| **R2** | 搜索索引「优化/回收」`OptimizeSearchIndex` | P2 | 只有重建、无回收（PRO `A----------.cs:30514` 返回 `(Reclaimed,Bytes)?`） | ✅ 仍缺 |
| **R3** | 截图后处理多选 `ImageCapturePostProcessMode` | P3 | ABSENT（PRO `Options/MediaInfo/MediaInfoExtractOptions.cs:307-341`） | ✅ 仍缺 |
| **R4** | 启动清 TMDB 最近缓存 `ClearRecentTmdbCacheOnStartup` | P3 | ABSENT（PRO `Options/Metadata/MetadataEnhanceOptions.cs:748` + `Plugin.cs:1772/1796`） | ✅ 仍缺 |
| **R5** | 追更最小延迟秒粒度 `CatchupMinimumDelaySeconds` | P3 | 分钟粒度、功能等价（PRO 该项本身 `[Browsable(false)]`） | ✅ 可不做 |
| **R6** | 跨库多版本播放进度 `LibraryScopePlayProgress` | P4 | ABSENT，且须改 `PresentationUniqueKey`（高风险） | ✅ 单独立项 |
| V1 | 搜索特殊符号 | — | PRO 侧在加密段内不可还原 → 语料驱动补漏 | ✅ 维持 |
| V2 | MovieDb 1.9.9.0 兼容 | — | 本仓反射+版本分层天然容错 → 只需实机验证 | ✅ 维持 |

**本轮额外确认的「非缺口」**（避免误判为缺失）：

| 项 | 结论 |
|---|---|
| MdbList 评分 | ✅ **本仓已实现**：`Provider/Douban/DoubanAssist.cs:38 UpdateMdbListRatingsAsync`（`MdbListBaseUrl` / `MdbListResponse` / `MdbListRating` 内联） |
| `TriggerMergeMovieTask` | ✅ 本仓已实现（`ScheduledTask/MergeMultiVersionTask.cs:21`） |
| 片头标记保护 | ✅ 本仓用 `Mod/ChapterChangeTracker.cs:13` 补丁实现（PRO 用 `IntroMarkerProtector` Provider） |
| IntroDB 刮削 | ✅ 本仓用 `Common/IntroDbApi.cs` + `ScheduledTask/ScrapeIntroDbTask.cs` 实现 |
| 外部字幕轨道扫描 | ✅ 本仓用 `ScheduledTask/ScanExternalSubtitleTask.cs` 实现（PRO 为 `ScanExternalTrackTask`） |

---

## 三、架构性差异（**不建议对齐**）

| 差异 | PRO 做法 | 本仓做法 | 判定 |
|---|---|---|---|
| 前端注入 | 11 个独立 `Get*Js.cs` 端点，逐个受 License 门控（`Web/Service/ScriptInjectService.cs:82-116`） | 1 个合并 JS（`Web/Resources/strmassistant.js`）+ `Web/Helper/ShortcutMenuHelper.cs` 统一注入 `shortcuts.js` | **不对齐**。本仓已实测注入生效（`Douban badge bootstrap` 出现在 `web/modules/shortcuts.js`），拆分只会放大回归面 |
| 豆瓣服务承载 | 混淆类 `A----------/A----------.cs:76506`（+ DTO 层） | `Provider/Douban/DoubanAssist.cs` 单文件内联 | 见 D5（渐进） |
| 任务清单 | 14 个任务，含 `RefreshMetadataTask` / `RestartServerTask` / `UpdatePluginTask` | 11 个任务，用 `RefreshEpisodeTask` / `RefreshPersonTask` / `MemoryGuard` / 主动删除自更新替代 | **不对齐**（功能等价或社区更优） |
| 授权 | `Portable.Licensing` + SystemId 绑定 + 网卡指纹 + 多实例互斥 | 无 | **不移植**（商业逻辑） |
| 跨实例同步 | `POST /Items/SyncMediaInfo` + SAP 局域网发现 | 无 | **不移植**（需自建写回协议） |

---

## 四、社区反超面（**绝不能为「对齐」误删**）

| 模块 | 说明 |
|---|---|
| `Mod/` 全层（39 个 Harmony 补丁） | PRO 无对应文件，社区自研 |
| `Core/` 适配层（11 类） | `EmbyVersionAdapter` / `FastReflection` / `HealthCheck` / `MemoryGuard` / `PerformanceMonitor` / `ConflictDetector` … 社区自研 |
| 影音直链 `DirectLinkOptions` | strm 302 + Follow302 + 客户端黑名单 + LiveTv 直链；**PRO 完全没有** |
| 内存运维 | `EnableMemoryAutoRestart` 倒计时 + `EnableMemoryCleanup` + `RuntimeStatusList`，比 PRO `MemoryOptions` 更全 |
| 豆瓣细分开关 | `DoubanCastChinese` / `DoubanPosterFallback` / `DoubanRatingOverride` / `RatingUpdateLookbackDays`（`Options/DoubanAssistOptions.cs:54/76/82/88`）——**PRO 没有这组细分** |
| ISO 用户开关 | `EnableIsoExtraction`（PRO 内建无开关） |
| 独有 API | `GetDoubanRatings`（评分徽章端点）/ `GetMultiVersionInfo`；**PRO 无对应端点** |
| 独有模块 | `IntroSkip/PlaySessionMonitor`、`ScrapeProxyWhitelist` |

---

## 五、执行顺序建议

```
┌ 阶段 0 · 立即（P1，半小时内可完成并验证） ────────────────────┐
│  · D1 移植 DoubanExternalId（≈30 行，零风险，用户直接可见）    │
│  · D2 修复评分快照缓存命中漏记（≈3 行）                        │
│  → 一起构建部署重启，实测 ExternalUrls 出现 Douban + 徽章回归  │
└──────────────────────────────────────────────────────────────┘
                              ↓
┌ 阶段 A · 豆瓣能力补齐（P2） ─────────────────────────────────┐
│  · D4 TriggerPopulateDoubanIdTask（带开关，默认 off）          │
│  · D3 季级豆瓣映射（季列表接口 + 映射表 + 回退标题搜索）        │
│  · R2 搜索索引「优化/回收」                                    │
│  · R1 筛选菜单 HasDoubanId / HasImdbId                         │
└──────────────────────────────────────────────────────────────┘
                              ↓
┌ 阶段 B · 小配置项（P3） ─────────────────────────────────────┐
│  · R4 ClearRecentTmdbCacheOnStartup                            │
│  · R3 ImageCapturePostProcessMode                              │
│  · R5 CatchupMinimumDelaySeconds（可不做）                     │
└──────────────────────────────────────────────────────────────┘
                              ↓
┌ 阶段 C · 重构与高风险（P3/P4，单独立项） ─────────────────────┐
│  · D5 豆瓣 DTO 化 + 限制级/黑名单（渐进，双通道并存）           │
│  · R6 LibraryScopePlayProgress（先专项验证 PresentationUniqueKey）│
└──────────────────────────────────────────────────────────────┘
                              ↓
┌ 阶段 D · 验证型（随时） ─────────────────────────────────────┐
│  · V2 MovieDb 1.9.9.0 实机验证                                 │
│  · V1 搜索特殊符号语料驱动补漏                                 │
│  · ISO 残留：真碟多分支选片 / Linux 蓝光 ISO 直读              │
│  · 多版本「画质+体积」：需真实多版本样本补测                    │
└──────────────────────────────────────────────────────────────┘
```

---

## 六、工程红线（必须遵守）

1. 技术栈锁 **`net6.0`**；**不引** PRO 私有依赖（`Portable.Licensing` / `BlurayMounter` / `BdInfoExaminer` / `MonoMod`）。
2. 新增第三方依赖须 MIT/BSD/Apache，并同步 `csproj` 的 **ILRepack** 合并参数。
3. **三份 resx（`Resources.resx` / `.zh.resx` / `.zh-hant.resx`）+ `Properties/Resources.Designer.cs` 四处必须同步**——`DisplayNameL`/`DescriptionL` 走反射查 `Resources` 同名 public static string 属性，**漏改 Designer 会导致配置页整页空白（前端 500）**。
4. **不得改写 `MediaSourceInfo.Path`**（播放路径与条目身份红线）。
5. 枚举本地化用基类 `DescriptionAttribute`，不依赖 Emby 4.10 的 `EnumExtensions.GetDescription`。
6. 版本适配统一走 `Core/EmbyVersionAdapter`。
7. 插件部署后**必须重启 Emby** 才加载新 DLL。
8. **豆瓣相关改动必须加限流**：复用 `RequestInterval`(2s) 与断路器（`BreakerThreshold=5` / `BreakerCooldown=6h`，`Provider/Douban/DoubanAssist.cs:142-145`），不得引入无节流的批量请求。

---

## 七、证据表（本轮直接读代码/实测所得）

| 结论 | 证据 | 等级 |
|---|---|---|
| 本仓**无** `DoubanExternalId` | `grep -rn "IExternalId" StrmAssistant/` → 仅 `Provider/MovieDbEpisodeGroupExternalId.cs:8` | 实证（高） |
| 条目页**无**豆瓣链接 | 实测 `GET /Items/191?Fields=ExternalUrls` → IMDb/TheMovieDb/Trakt（无 Douban） | 实机实测（高） |
| PRO 有 `DoubanExternalId` | `StrmAssistantPro.decompiled/StrmAssistant/Provider/DoubanExternalId.cs:11`（`IExternalId, IHasWebsite`） | 源码实证（高） |
| 本仓快照在缓存命中时漏记 | `Provider/Douban/DoubanAssist.cs:976-977`（早退）vs `:1021`（仅新取时记录） | 源码实证（高） |
| 本仓**无**季映射 | `grep DoubanSeasonsResponse` 0 命中；`BuildDoubanCacheTask.cs` 仅 110 行；季匹配仅 `DoubanAssist.cs:798-835` 标题搜索 | 源码实证（高） |
| 季匹配实际全失败 | 日志 `DoubanAssist - No douban match for: 第 1 季` ×≈20 | 实机实测（高） |
| PRO 有季映射 | `BuildDoubanCacheTask.cs:4219 UpdateSeasonMap` + `Dto/Douban/DoubanSeasonsResponse.cs:190 is_released` | 源码实证（高） |
| 本仓**无** `TriggerPopulateDoubanIdTask` | `grep ILibraryPostScanTask` → 仅 `MergeMultiVersionTask.cs:21` | 实证（高） |
| PRO 有该任务 | `ScheduledTask/TriggerPopulateDoubanIdTask.cs:12`（`ILibraryPostScanTask`） | 源码实证（高） |
| 本仓**无** DTO 层 | `Provider/Douban/DoubanAssist.cs` 单文件 1325 行，正则解析（`:218-236`） | 源码实证（高） |
| 本仓 MdbList **已实现** | `Provider/Douban/DoubanAssist.cs:38 UpdateMdbListRatingsAsync` | 源码实证（高） |
| 徽章注入**生效** | `GET /web/modules/shortcuts.js` 含 `Douban badge bootstrap` ×2（19770 B） | 实机实测（高） |
| BUG-1 修复稳定 | 日志 `risk control` 0 / `400` 0；评分端点正常返回 3 条 | 实机实测（高） |
| PRO 全量 139 个 `.cs` 已盘点 | 文件级核对表 100% 覆盖 | 方法论（高） |
| PRO 具体 URL/端点**不可读** | ConfuserEx 字符串加密；本方案一律用语义锚点（属性名/枚举名/资源键/路由） | 方法论（高） |

---

## 附：与 v10 的关系

- v10 是**非豆瓣面**的权威基线（R1~R6 + V1/V2 + 不移植清单 + 红线）——**本版全部继承，未做任何推翻**。
- v11 **新增**豆瓣面 D1~D5，并修正 v10「整体对齐度 95%」这一**未单列豆瓣面**得出的乐观结论。
- 两版合并使用的正确姿势：**做非豆瓣项看 v10，做豆瓣项看 v11**。
