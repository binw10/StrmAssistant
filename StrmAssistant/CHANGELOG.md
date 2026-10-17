# 更新日志 / Changelog

## [未发布]

### ✨ 界面优化
- **设置页结构重排为 6 个 Tab**：
  - 「神医助手 Plus」更名为 **「基础设置」**（插件品牌名仍用于通知标题），页面顶部新增**插件总开关**。关闭后 `Plugin.Initialize` 整段跳过（不加载补丁、不订阅事件、不初始化队列），定时任务与 Web 端点同步早退；**修改后需重启服务器生效**——不做即时生效是因为撤销 36 个 Harmony patch 并逐个验证幂等性的风险高于收益。
  - 「体验增强」更名为 **「高级功能」**，位置移到「基础设置」之后。
  - 新增 **「网络代理」** Tab（第 6 个），收纳原先分散在首页与元数据增强页的三块配置：网络（代理 / 本地发现）、影音直链、TMDB 替换。
  - 「备用 MovieDb 接口」更名为 **「TMDB 替换」**。
  - 「Mod 功能」更名为 **「中文搜索」**，其下分组为「匹配与范围」。
- **媒体信息 · 设置分组**：原来 16 项平铺设置，改为 **5 个带小标题的分组**——提取范围 / 图像捕获 / ISO 提取 / 信息持久化 / 性能与独占。
  - `启用 ISO 提取` 与 `自定义 ffprobe 路径` 归入同一组：Linux/Docker 下 ISO 由自定义 ffprobe 直读 UDF，两者本是一件事，此前分处页面两端。
  - `媒体库范围` 由页面靠后位置移到「提取范围」组首位，与 4 个排除项（关键词/容器 × 媒体信息/缩略图）归拢。
  - 老版本平铺 JSON 配置会**自动迁移**为嵌套结构，原文件备份为 `StrmAssistant_MediaInfoExtractOptions.json.legacy.bak`。
  - 迁移后首次保存时，若 `LibraryList` 尚未初始化（启动期保存），不再把已选媒体库范围清空。
- **片头片尾 · 设置分组**：18 项平铺改为 **3 个分组**——声纹探测 / 行为探测 / 在线刮削。两个总开关不再分居页面首尾。
- **体验增强 · 设置分组**：15 项平铺改为 **5 个分组**——多版本合并 / 多版本展示 / 播放与清理 / 通知 / 界面功能。
- **基础设置 · 首页重组**：
  - 「通用选项」拆为 **任务并发 / 内存管理 / 运行状态与维护** 三组，`CatchupTaskScope` 不再与 `CatchupMode` 相隔十余行；维护操作按钮统一归入「运行状态与维护」组末。
  - 「Mod 功能」更名为 **「中文搜索」**（Mod 是开发者术语，用户无法从字面看出内容）；「直链重定向」组已迁出至「网络代理」页。
  - 「显示媒体信息」由搜索组迁入「体验增强 · 界面功能」。
- **卡片海报豆瓣角标移除**：详情页评分行已承载「豆瓣 logo + 分数」，卡片右上角的「豆 7.8」属于重复信息，且 absolute 定位会遮挡封面标题区域。
- **基础设置 · 顶部品牌区**：页面最顶依次为免责声明按钮、「神医助手 Plus」组头与版本行（自动读取程序集版本，星形徽章图标，Support 4.10.1.0，启动准入仍保留 4.9.1.0）。「通用」「匹配与范围」两个分组小标题移除，插件总开关描述精简。

> 以上分组改造均带配置迁移：旧平铺 JSON 会就地转为嵌套结构，原文件备份为 `*.legacy.bak`。
> 例外：「显示媒体信息」因跨配置文件迁移（从主配置到体验增强配置），升级后需在「体验增强 · 界面功能」中重新开启（默认 OFF）。
> 「网络代理」的迁移需读取两个旧文件（`StrmAssistant.json` 的 `NetworkOptions`/`DirectLinkOptions`、`StrmAssistant_MetadataEnhanceOptions.json` 的 `AltMovieDb`），任一缺失时对应分组回退默认值。

### 🐛 修复
- **剧集「第 N 季」豆瓣匹配全部失败**：根因是豆瓣 suggest 把剧集与季都标为 `type=movie`，而代码按 `type=="tv"` 严格过滤，候选被整批丢弃（日志中 `No douban match for: 第 1 季` 反复出现）。
  - 类型过滤改为「仅排除非影视类型」（接受 `movie` / `tv`）。
  - 季匹配新增**季号强校验**：候选标题必须包含「第N季 / 第二季 / Season N / Part N / 第N部」之一，避免第 2 季误配第一季。
- **新增季级映射表**（等价 PRO 的 `UpdateSeasonMap`）：`strmassistant_douban_seasons.json` 按「剧集豆瓣 ID → 季号 → 季豆瓣 ID」缓存，同一部剧的多个季只需解析一次，后续季条目零搜索直接命中；条目失效（下架/改 ID）时自动重新搜索并回填；缓存 7 天过期。
- **季匹配标题主体校验**：仅校验「标题含季号」仍会错配——豆瓣对「重器 第1季」只返回「大国重器 第一季」（2013），会把完全不同的作品当成本剧的季。现要求候选标题去掉季标记与标点后**与剧名主体完全相等**，不满足即放弃并走 S1 回退（单季剧回退到剧集 ID，结果正确）。实测：「重器」由错误的 25762332 纠正为 36554071，评分从无变为 7.3。
- **单季剧季条目回退**：豆瓣对单季剧不建独立季条目（剧集主条目即第一季页），S1 在搜索无果时回退复用剧集豆瓣 ID；S2+ 不做此回退，避免拿主条目冒充。
- **季条目纳入「扫库后填充豆瓣 ID」**：`IncludeItemTypes` 增加 `Season`，并修正季条目此前传 `seasonNumber=null`（会被当成剧集主条目匹配）的问题；`BuildCacheAsync` 同步修正。
- **豆瓣评分图标误标**：评分行此前靠「页面数字 ≈ 豆瓣分（±0.05）」猜测来源，当 TMDB 与豆瓣同分（如都为 8.0）时会把 TMDB 分误标成豆瓣 logo。
  - 评分快照新增 `Applied` 标记：仅在「豆瓣评分覆盖」开启、评分字段勾选、未超出回看期且 `CommunityRating` 确实写入豆瓣分时置位；徽章端点透出该字段，前端据此决定是否替换图标，数值比对降为二次校验。
  - 旧快照无该字段（`null`）时退回实测推断，升级后图标不会集体回退。
  - 快照写盘按 10 秒节流，批量任务收尾强制落盘，避免全量填充时逐条写文件。
- **豆瓣评分跳过原因不可见**：六道门控（条目锁定 / 语言非中文 / 评分字段未勾选 / 覆盖开关关闭 / 超出回看期）此前只留一行含糊日志。
  - 统一为 `Skipped (reason=...)` 结构；「扫库后填充豆瓣 ID」任务结束时额外输出一行分布汇总（`total / matched / noMatch / 各门控跳过数`），可直接回答「为什么有些条目没有豆瓣评分」。
- **`Rating updated` 日志参数重复**：原先打印的是「新值、新值」，改为「旧值 → 新值」。
- **扫库后填充豆瓣 ID 改为增量 + 重试**：此前每次扫库都会全量遍历所有电影/剧集。现在只处理缺失 ID 的条目，失败条目落盘 `strmassistant_douban_retry.json` 优先重试（单条上限 3 次，超限移出队列），任务日志新增 `Candidates` 与 `Retry queue` 统计。
- **前端 DOM 扫描合并**：豆瓣徽章 / 多版本 / 界面功能三个子模块原先各自持有 `MutationObserver` + 600ms 防抖计时器，一次 DOM 变更会触发三次全量扫描；现改为单一 observer + 单一防抖，各模块注册扫描函数串行执行。

### 🔧 功能优化
- **设置页选项搜索面板**：页面左上角注入浮动搜索框，输入即按选项 label 实时过滤分组；对快捷键 `/` 或中文搜索键响应；ESC 清空、回车跳转首个匹配项高亮。解决 6 Tab × 30+ 分组难以定位的问题。
- **设置页操作失败友好提示**：服务端重启后旧页面点保存/启动按钮会报 `No active setup stage`（500）。前端拦截该响应，提示「服务器已重启，请刷新页面后再试」，附带刷新链接。
- **缩略图前置条件提示**：`媒体库范围` 描述中追加说明——提取视频预览缩略图前，需先在 Emby 媒体库设置中把「生成视频预览缩略图」设为计划任务。
- **清标语文案修正**：中文「清标语」→「清除标语」、繁中「清標語」→「清除標語」。
- **季映射清理按钮**：豆瓣辅助设置页新增「清除季映射」按钮，一键删除 `strmassistant_douban_seasons.json` 中全部缓存映射，使季条目在下次刮削时重新匹配。用于季条目被错误映射后的手动重置。
- **强制重新提取标记**：`ClearMediaInfo`（维护操作「清除媒体信息」）后，被清理的条目会被标记为「需要重新提取」并落盘（`strmassistant_force_reextract.txt`）；提取任务取数时把标记条目补回（否则会被 DB 查询条件排除）、探测流程跳过「DB 已有媒体信息」捷径，确保清缓存后能真正重新探测并重新写 json，不再报 `Number of items: 0`。
- **Tidb 刮削日志聚合**：逐集请求失败（404 无数据 / 超时 / 连接失败）原日志一次任务可产生 90+ 条 `Warn`。现改为：404 无数据降为 Debug 并计数；网络失败首条 `Warn` 提示、后续降为 `Debug`；任务收尾聚合输出单行汇总（如 `Request failures: timeout=62, other=30`、`no data for 26 episode request(s)`）。实测一次任务 Warn 从 92 条降为 1 条。
- **豆瓣匹配置信度标记**：匹配日志增加路径标记——直接命中 → Debug，映射表命中 → Info，季搜索主体校验拒绝 → Info（附拒绝原因），S1 回退 → Info。便于排查「为何没匹配到」或「为何匹配错」。
- **Resx/Designer 键一致性校验**：新增 `tools/check_resx_keys.py`，构建前自动检查三语言 resx 与 `Resources.Designer.cs` 的键是否一致，防止「加了 resx 键但忘了同步 Designer」导致页面 500。
- **HDR Vivid 支持（对齐 PRO）**：补齐此前缺失的上游检测——探测 postfix 读取 ffprobe `side_data_list` 中 `side_data_type` 含 `CUVA`（`HDR Dynamic Metadata CUVA 005.1 2021 (Vivid)`）的视频流，命中后固化写入 `MediaStream.Comment = "HDR Vivid"`（`Comment` 是 Emby 公开可写且**可持久化**的字段；`VideoRange` 为只读计算属性，且 Emby 的 `ExtendedVideoTypes` 枚举无 Vivid 成员，故不可用）。同时 Prefix `MediaStream.GenerateVideoRange(bool isForDisplay)`，使 Emby 原生「媒体信息」区显示 `HDR Vivid`。多版本标签侧改为读 `Comment` 并**优先于 DV/HDR10+/HDR 类型分支**，解决「双层 DV P7 + Vivid 元数据共存」时的标签歧义。此前的 Vivid 分支读 `videos[0].VideoRange` 属死代码（Emby 只会把它算成 `HDR`/`SDR`），实际永不命中。

## [3.0.0.55] - 2026-10-16

### ✨ 界面优化
- **版本号显示修正**：不再拼接 git commit hash，直接显示 `3.0.0.55`。
- **体验增强 · 版本显示方式 / 版本排序方式**：由自由文本改为**中文下拉框**（默认 / 画质 / 画质+体积 / 文件名；默认 / 画质降序 / 画质升序 / 路径升序 / 路径降序）。属性由 `string` 改为枚举，旧配置值（`MediaInfo`、`SystemDefault` 等）与枚举成员同名，可原样反序列化。
- **多版本 · 新增「画质+体积」显示格式**：标签同时给出画质档位、视频编码、码率与文件体积，形如 `4K HEVC 46.1Mbps 168.4MB`；缺项自动省略。多版本详情页排序同步按标签首个档位词识别（此前复合标签会被判为最低档）。
- **元数据增强 · 设置分组**：原来约 40 项平铺设置，改为 **9 个带小标题的分组**——中文元数据 / 剧集组 / 刮削优化 / 人物增强 / 豆瓣辅助 / 海报与排序 / 元数据刷新 / 本地元数据缓存 / 备用 MovieDb 接口。
  - 老版本平铺 JSON 配置会**自动迁移**为嵌套结构，原文件备份为 `StrmAssistant_MetadataEnhanceOptions.json.legacy.bak`。

### 🐛 修复
- **版本号方案统一为四段式 `3.0.0.55`**：与 Releases tag 体系一致（此前为日期式 `26.3.25`、三段式 `3.0.55`）。同时修正因编号方案变更导致「中文搜索升级检查」把 `3.0.0.55` 误判为**降级**的问题——标记文件现额外记录编号方案号，方案变更时只重置基线，不再误报。
- **体验增强页 500 错误**：`Resources.Designer.cs` 缺少 8 个枚举标签属性（`MultiVersionDisplayOption_*` / `MultiVersionSortOption_*`），而 Emby 的 `[DescriptionL]` / `[DisplayNameL]` 是通过**反射查找 `Resources` 的公共静态属性**解析的（不是走 `ResourceManager`），因此仅把键写进 `.resx` 不够。已补齐 8 个属性，页面恢复正常。
  - 说明：`Resources.Designer.cs` 由 `PublicResXFileCodeGenerator` 生成，但 **`dotnet build`（命令行 MSBuild）不会运行该设计期生成器**，新增资源键后必须手工同步该文件。
- **豆瓣演员中文名未覆盖客串演员**：中文化过滤条件仅取主演（`PersonType.Actor`），导致客串（`GuestStar`）演员的中文名始终不被写入。现同时覆盖两者。

## [26.3.25] - 2026-03-25

### 🔧 修复与调整
- ARM64 下所有 `Harmony not implemented on this arm64 target` 日志降级为 **Debug** 输出，避免常规日志刷屏。
- 版本号更新为 `26.3.25`（程序集版本 `26.3.25.0`）。
- 移除 `SuppressPluginUpdate` 相关功能，不再拦截或屏蔽插件更新列表。
- 运行要求明确为 **仅支持 Emby 4.9.1.0 及以上版本**。

## [2.2.0] - 2024-12-03

### 🎉 重大更新 / Major Updates

#### 全新架构系统 / New Architecture System
- **EmbyVersionAdapter**: 智能版本检测和适配系统
  - 自动检测Emby版本并映射到API版本枚举
  - 支持从4.8.0到4.10.0+的所有版本
  - 提供方法缓存和安全调用包装器
  
- **ServiceLocator**: 全局服务定位器
  - 统一的服务注册和获取机制
  - 支持类型服务和命名服务
  - 线程安全的并发实现

- **增强的PatchTracker**: 详细的补丁状态跟踪
  - 新增6种补丁状态（NotInitialized, Initializing, Initialized, Applied, Failed, NotSupported）
  - 错误消息收集和历史记录
  - 核心功能vs可选功能区分

#### 高性能优化系统 / High-Performance Optimization
- **FastReflection**: 高性能反射调用器
  - 使用Expression树编译技术，性能接近直接调用
  - 比标准反射快50-100倍
  - 自动缓存编译的委托，缓存命中率>95%
  - 支持方法调用和属性访问的优化
  
- **PerformanceMonitor**: 性能监控系统
  - 实时跟踪关键操作的执行时间
  - 统计调用次数、平均/最小/最大耗时
  - 自动识别慢操作（可配置阈值）
  - 生成详细的性能报告

- **HealthCheck**: 健康检查和自诊断
  - 自动检查所有核心组件状态
  - 检测性能问题和内存使用
  - 生成全面的诊断报告
  - 健康状态分级（Healthy/Degraded/Unhealthy/Critical）
  - 启动时自动执行健康检查

- **PerformanceReporter**: 定期性能报告
  - DebugMode下每60分钟自动生成报告
  - 包含FastReflection统计、Top 10操作
  - 自动检测慢操作和内存使用
  - 提供详细的系统健康状况

- **OptimizationAdvisor**: 智能优化建议
  - 根据运行状况提供优化建议
  - 按优先级分类（Critical/High/Medium/Low）
  - 自动分析性能瓶颈
  - 启动时检查并提示关键问题

### ✅ 完全支持 Emby 4.9.1.90 / Full Support for Emby 4.9.1.90
- 专门为4.9.1.90版本优化的API调用
- 支持最新的MediaSources API变化
- 支持最新的通知系统增强
- 向后兼容4.8.x和4.9.0.x所有版本

### 🔧 架构改进 / Architecture Improvements
- **三层回退机制**: Harmony → Reflection → PublicAPI
- **智能方法查找**: 自动适配不同版本的方法签名
- **性能优化**: 方法和类型信息缓存，减少反射开销
- **诊断增强**: 新增`GetDiagnosticReport()`方法，详细的系统状态报告

### 📝 代码质量改进 / Code Quality Improvements
- 更详细的日志记录和错误追踪
- 优雅的功能降级处理
- 更好的异常处理和错误恢复
- 代码结构更清晰，可维护性更强

### 📖 文档更新 / Documentation Updates
- 新增 `ARCHITECTURE.md` 架构文档
- 新增 `CHANGELOG.md` 更新日志
- 更新 `README.md` 支持版本信息
- 更新项目版本号为 2.2.0.0

### 🔄 API变化 / API Changes
- `EmbyVersionCompatibility` → `EmbyVersionAdapter` (旧类已弃用但保留兼容)
- 新增全局 `ServiceLocator` 用于服务管理
- `PatchTracker` 新增状态属性和错误收集方法
- `PatchManager` 新增诊断和缓存清理方法

### ⚠️ 破坏性变化 / Breaking Changes
无破坏性变化。所有现有功能保持完全向后兼容。

---

## [2.1.0] - Previous Release

### ✅ 增强的版本兼容性
- 动态检测并适配不同Emby版本的API方法签名
- 支持5参数和7参数两种GetStaticMediaSources方法签名
- 改进的反射调用错误处理和回退机制

### ✅ 健壮的路径获取
- 增强的IMediaMount路径获取，支持多种属性名
- 智能回退机制，确保功能可用性

### ✅ 优化的错误处理
- 详细的日志记录和调试信息
- 优雅的降级处理，单个模块失败不影响整体功能

---

## 版本支持矩阵 / Version Support Matrix

| 插件版本 | 推荐Emby版本 | 最低Emby版本 | 最高测试版本 |
|---------|-------------|------------|------------|
| 2.2.0   | 4.9.1.90    | 4.8.0.0    | 4.9.1.90   |
| 2.1.0   | 4.9.1.80    | 4.8.0.0    | 4.9.1.80   |

## 功能兼容性 / Feature Compatibility

| 功能 | 最低版本要求 | 说明 |
|-----|------------|------|
| IntroSkip | 4.8.0 | 片头片尾跳过 |
| EpisodeGroups | 4.9.0 | 剧集分组 |
| EnhancedMediaInfo | 4.9.1.80 | 增强媒体信息 |
| AdvancedFingerprinting | 4.9.1 | 高级指纹识别 |
| OptimizedMediaSources | 4.9.1.90 | 优化媒体源 |
| EnhancedNotifications | 4.9.1.90 | 增强通知系统 |

## 升级指南 / Upgrade Guide

### 从 2.1.0 升级到 2.2.0

1. **备份配置**: 建议先备份现有配置
2. **停止Emby服务**: `systemctl stop emby-server` 或通过服务管理器停止
3. **替换插件文件**: 替换 `StrmAssistant.dll`
4. **启动Emby服务**: 重新启动Emby服务
5. **检查日志**: 查看日志确认新架构初始化成功

### 配置迁移
无需配置迁移。所有现有配置自动兼容。

### 回滚方案
如遇问题可回滚到2.1.0版本：
1. 停止Emby服务
2. 替换为旧版本的DLL文件
3. 重启Emby服务

## 已知问题 / Known Issues

### v2.2.0
- 无已知严重问题

### 限制 / Limitations
1. Harmony ReversePatch在某些复杂IL代码上可能失败（会自动降级到Reflection）
2. 非X64架构不支持Harmony（会使用Reflection）
3. 某些私有API在未来Emby版本中可能发生变化

## 贡献者 / Contributors

感谢所有为本项目做出贡献的开发者！

---

**注意**: 本项目为开源社区版本，基于原始项目使用AI进行适配和优化。
