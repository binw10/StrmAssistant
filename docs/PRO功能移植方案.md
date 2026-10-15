# PRO 版功能移植与模块优化方案

> 目标项目：`D:\下载\AI\StrmAssistant-myp015`（分支 myp015，版本 26.3.25，target net6.0，Emby 4.9.1.90）
> 参考对象：PRO 版 v3.0.0.54（闭源付费，仅作功能形态参考）+ PRO Wiki 功能清单
> 约束：**全部自研实现**，不复制反编译混淆代码；本项目沿用社区版 GPLv3，衍生分发继续开源。

---

## 一、结论先行

**重要发现：`_pro_tmp\.trae\documents\` 里那份《PRO功能移植-分阶段实施计划.md》规划的 P1–P4 已经全部落地了。**

核对结果：

| 原计划阶段 | 落地证据 |
|---|---|
| P1 前端 UI 增强 | `Options/UIFunctionOptions.cs`、`Web/Api/GetStrmAssistantJs.cs`、`Web/Service/ShortcutMenuService.cs`、`Web/Helper/ShortcutMenuHelper.cs` 齐备 |
| P2 元数据刷新增强 | `ScheduledTask/RefreshEpisodeTask.cs`、`RefreshPersonTask.cs`、`MetadataApi.cs`（多源兜底 + LruCache） |
| P3 豆瓣刮削 | `Provider/Douban/DoubanAssist.cs`、`Web/Api/GetDoubanRatings.cs`、`MetadataEnhanceOptions.cs`（DoubanCookie / DoubanRatingOverride / DoubanCastChinese 等 12 个开关） |
| P4 IntroSkip 在线刮削 | `Common/IntroDbApi.cs`（api.theintrodb.org/v3）、`ScheduledTask/ScrapeIntroDbTask.cs`、`Options/IntroSkipOptions.cs` |

所以**不要再按旧计划重做**。本方案只针对 PRO Wiki 上仍缺失或不完整的部分。

---

## 二、功能矩阵（PRO Wiki × 你的实现）

### ✅ 已对齐（无需处理）

| 域 | PRO Wiki 功能 | 你的实现 |
|---|---|---|
| 元数据 | 豆瓣辅助刮削 | `Provider/Douban/DoubanAssist.cs` |
| 片头 | 片头片尾刮削 Unified IntroDb | `Common/IntroDbApi.cs` + `ScrapeIntroDbTask` |
| 多版本 | 美化显示 / 画质排序 | `Web/Service/MultiVersionService.cs`（DoVi / HDR10+ / 分辨率解析） |
| 体验 | 快捷菜单（清媒体信息 / 章节图 BIF / 重复演员） | `Web/Api/` 下 ClearMediaInfo / ClearChapterImages / DeleteVersion |

### ⚠️ 部分实现（需补完，投入小）

| # | 缺口 | 现状 | 目标 |
|---|---|---|---|
| A1 | **各媒体库自定义声纹探测时长** | `IntroSkipOptions.IntroDetectionFingerprintMinutes` 仅全局值 | 改为支持 `LibraryId->分钟` 映射表，检测时按库取值 |
| A2 | **跨库多版本独立进度 / 收藏** | `MergeMultiVersion` 已合并展示，但无独立进度隔离 | 按 LibraryId 分桶存进度与收藏 |
| A3 | **解除同目录合并 8 部上限** | 未找到相关硬编码 | 定位合并逻辑上限，改为可配置 |
| A4 | **通知：合集变更 / 元数据更新 / 图像更新** | `CustomNotifications.cs` 仅 2 个通知类型 | 补 3 个 `NotificationTypeInfo` + 触发点 |
| A5 | **主屏媒体库项目数量标记** | 无 | 注入 JS 增补 |

### ❌ 完全缺失（按价值/难度排序）

| # | 功能 | 价值 | 难度 | 依赖 |
|---|---|---|---|---|
| B1 | 简繁转换 + 中文搜索增强重构 + 屏蔽搜索推荐 | 高 | 中 | 需换 sqlite3.dll + libsimple |
| B2 | 本地 TMDB 元数据刮削源（离线刮削） | **很高** | 中高 | MovieDbSeriesProvider 范式 |
| B3 | 媒体信息关键字黑名单 | 中 | 低 | 现成 `FingerprintBlacklistKeywords` 范式 |
| B4 | 提取 ISO 媒体信息（多线程 ISO/BDMV） | 中高 | 高 | ffmpeg/ffprobe 静态版 |
| B5 | 自定义 ffmpeg 路径（修 DV P5 截图发绿） | 中 | 低 | 配置项 + 路径解析 |
| B6 | 外挂音轨扫描（需 4.9） | 中 | 中 | LibraryApi 扩展 |
| B7 | OpenSubtitles 搜索下载优化（为 Strm） | 中 | 中 | HTTP 节流范式 |
| B8 | 音乐持久化 json（专辑/艺术家/内嵌封面） | 中 | 中 | PersistMediaInfoTask 扩展 |
| B9 | 视频截图后处理（16:9 / 2:3 最优比例） | 中 | 低 | 已有 ImagePostProcessor |
| B10 | 分布式媒体信息提取 | 高（多节点） | **很高** | 节点间通信协议 |
| B11 | Bangumi 外部 ID + 备选语言优先繁中 + 新建库默认 TMDB | 低 | 低 | Provider 范式 |
| B12 | 代理「刮削白名单 / 全局代理」双模式 | 中 | 低 | EnableProxyServer 扩展 |

---

## 三、实施路线

### P0 · 低风险体验补完（建议先做，1 轮可完成）

目标：把 ⚠️ 类的 5 项补齐，纯配置项 + 少量 UI 注入，不动核心提取逻辑。

- **A1** `IntroSkipOptions` 增加 `Dictionary<string,int> LibraryFingerprintMinutes`；`ExtractIntroFingerprintTask` 按 Item.MediaSource 所属库查表回落全局值
- **A3** `MergeMultiVersion` 找到合并上限，暴露 `MergeMaxMovieCount` 配置（默认 8，向后兼容）
- **A4** `CustomNotifications.GetNotificationTypes` 追加 3 个类型；在 `Plugin.OnItemUpdated` 按 ItemChangeType 分派
- **A5** `strmassistant.js` 增加 Dashboard API 拉取 + 首页角标渲染
- **B3** 复用 `IntroSkipOptions.FingerprintBlacklistKeywords` 的模式，给 `MediaInfoExtractOptions` 加 `ExtractKeywordBlacklist`，在 `ExtractMediaInfoTask` 入口过滤
- **B5** `MediaInfoExtractOptions.CustomFfmpegPath`，替换 `MediaInfoApi` 内的 ffprobe 调用路径
- **B12** `NetworkOptions` 加 `ProxyMode` 枚举（None / ScrapeOnly / Global）+ `ScrapeHostWhitelist`
- **B11** `Provider/BangumiExternalId.cs` 照 `MovieDbEpisodeGroupExternalId` 写

改动面小、可独立验收。

### P1 · 搜索与字幕

- **B1** 三件事：
  1. 简转繁/繁转简：项目已引 `CHTCHSConv`（ILRepack 已合并 `ChineseConverter.dll`），只需在 `EnhanceChineseSearch.cs` 接线即可，无需新依赖
  2. 简繁混搜：`libsimple.so` 已升级 v0.7.1，需确认分词器对繁体不误切
  3. 屏蔽搜索推荐：注入 JS 拦截搜索页推荐区块
  ⚠️ **前置**：`EnhanceChineseSearch` 依赖 P/Invoke `libsimple`，Emby 4.9.1.1+ 换了自编译 sqlite，**必须替换 `%appdata%\Emby-Server\system\sqlite3.dll`** 为带附加函数的版本，否则功能静默失效。仓库 `sqlite/` 目录已有 linux/osx 构建产物，Windows 版需另取。
- **B6/B7** `ScanExternalSubtitleTask` 加音轨维度；`SubtitleApi` 走 `GeneralOptions.Tier2MaxConcurrentCount` 节流，Strm 场景需先 HEAD 探测大小

### P2 · 刮削源扩展（长期最高价值）

- **B2 本地 TMDB**：仿 `MovieDbSeriesProvider`，把 TMDB 响应导出为本地 json/缓存目录，`RemoteName` 指向本地路径。收益：TMDB 限流/断网/删 API 时仍能刮削，且可离线批量预热。
  落地要点：需自建 `Provider/LocalTmdbSeriesProvider.cs` + `LocalTmdbStore.cs`（磁盘缓存，参考 `DoubanAssist` 的持久化思路）；新增 `MetadataBuildOptions` 分区配置源路径与刷新策略。
- **B8 音乐持久化**：现有 `PersistMediaInfoTask` 只处理视频，需为 `MusicAlbum` / `MusicArtist` / `Audio` 建 json 序列化与恢复路径（专辑封面即内嵌图）。
- **B9 截图后处理**：已有 `Common/ImagePostProcessor.cs`，接入 `EnableImageCapture` 链路，按 `ForcePortraitPoster` / `EnsureEpisode16x9` 决定裁切目标比例。
- **A2 跨库进度**：`MergeMultiVersion` 场景下，进度与收藏按 LibraryId 分键存储，需确认 Emby 的 `PlaybackProgressManager` 是否有可扩展键位；无则自建映射表。

### P3 · 重型能力（后置）

- **B4 ISO 提取**：需 ffprobe 对 ISO/BDMV 虚拟挂载能力。多线程部分建议用 `rffmpeg` 分段探测 + 合并，避免整盘读取。必须用仓库 `static-ffprobe/` 的通用静态编译版，官方/amilys 镜像的 ffprobe 不支持。
- **B10 分布式提取**：需要节点注册、心跳、任务分片、断线重传。属于架构级新增，建议独立设计文档后再动手；建议先做「单机多线程提取」作为过渡收益版本。

---

## 四、风险与注意事项

1. **sqlite 依赖是隐形炸弹**：B1 的中文搜索在 4.9.1.1+ 必须替换 `sqlite3.dll`，否则 P/Invoke 静默失败、看起来"功能没生效"。移植前先确认部署环境。
2. **新版 Emby 测试版兼容**：你已只支持 4.9.1.90（CHANGELOG 明确移除 4.8 支持），这与 PRO 一致（PRO 支持到 4.9.1.90+）。继续保持单一基线，不要回退多版本兼容层。
3. **写回冲突**：豆瓣/本地 TMDB 都可能覆盖内置刮削。继续沿用你现有的「仅补空 + 字段级合并」策略（`DoubanRatingOverride` 已是显式开关，保持这个粒度）。
4. **本地化同步**：新增配置项必须同时补 `Properties/Resources.resx` / `.zh.resx` / `.zh-hant.resx` 并与 `Resources.Designer.cs` 同步，否则编译失败。
5. **技术栈约束**：net6.0 下统一用 Emby 的 `IHttpClient` + `IJsonSerializer`，不引入 `HttpClient` / `System.Text.Json`。
6. **ILRepack 合并产物**：`build-local.sh` 与 csproj 的 `PostBuildMerge` 会把 `0Harmony.dll` / `ChineseConverter.dll` / `TinyPinyin.dll` 合并进主 dll 并自动部署到 `%APPDATA%\Emby-Server\programdata\plugins`。新增依赖库时**必须同步改 ILRepack 参数**，否则运行时缺 dll。

---

## 五、建议的下一步

从 **P0** 开始 —— 8 个小改动，风险低、可独立验收，落地后用户能立刻感知到差异（快捷菜单、通知类型、配置粒度都变丰富）。

P0 完成并验收后，再进 **P1**（需先解决 sqlite 部署）或 **P2 本地 TMDB**（价值最高）。

需要我从 P0 的哪一项开始动手？建议顺序：A3 → B5 → B12 → B3 → A1 → A4 → A5 → B11（从纯配置项到 UI 注入逐步推进）。