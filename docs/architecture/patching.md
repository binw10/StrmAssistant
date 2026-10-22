# 补丁模型

所有对 Emby 行为的改动都走 Harmony 补丁，集中在 `Mod/`。`Common/` 里放真正的业务逻辑，
补丁只负责"在正确的时机把活交出去"。

## 三个基础设施

| 类型 | 作用 |
|---|---|
| `PatchBase<T>` | 每个功能一个单例（`Instance`），提供 `Patch()` / `Unpatch()`，子类实现 `OnInitialize()` 与 `Prepare(bool apply)` |
| `PatchTracker` | 记录该补丁的状态：`DefaultPatchApproach`、`FallbackPatchApproach`、`IsSupported`、`Status`、`ErrorMessages`、`IsCoreFeature` |
| `PatchUnpatch(tracker, apply, method, prefix/suffix, suppress)` | 统一的打/撤补丁入口，内部按 `PatchApproach.Harmony` → `Reflection` 逐级回退 |

`PatchApproach` 有三档：`Harmony`（首选）、`Reflection`（Harmony 不可用时的兜底）、`None`（关闭）。
`PatchTracker.FallbackPatchApproach = PatchApproach.None` + `IsSupported = false` 是"这套 Emby 版本上不启用该功能"的标准写法。

**幂等性**：`Patch()` 可重复调用，`Unpatch()` 必须能把状态清干净。`EnablePlugin` 总开关走的是
"重启后生效"而不是即时生效，原因就在这里 —— 撤销 37 个补丁并逐个验证幂等性，漏一个就会出现
"关了其实没关"，比没有开关更糟。

## 补丁清单（37 个）

按作用面分组，`Mod/` 下的类名即文件名。

### 直链与播放
| 类 | 作用 |
|---|---|
| `StrmDirectRedirect` | strm 视频/音乐 302 直链（含 LiveTV 频道），可选跟随网盘 302，支持客户端黑名单回退中转 |
| `LiveTvDirectRedirect` | LiveTV 源标记为可直连，配合直链实现 302 |
| `EnableProxyServer` | 网络代理（`CreateHttpClientHandler` 替换） |
| `CustomLocalDiscovery` | 自定义 UDP 本地发现地址（局域网客户端连不上服务器时用） |

### 中文搜索与排序
| 类 | 作用 |
|---|---|
| `EnhanceChineseSearch` | FTS5 索引 + 中文分词器重建，链路见 [chinese-search.md](chinese-search.md) |
| `PinyinSortName` | 生成拼音排序名（`SortName`） |
| `OptimizeItemOrder` | 条目显示顺序（自然排序 / 拼音 / 首字母等四选） |
| `EnforceLibraryOrder` | 强制媒体库排序作用于所有用户 |
| `EnforceUserPreferences` | 强制用户显示偏好（`SeriesDisplay` 分支） |

### 元数据与刮削
| 类 | 作用 |
|---|---|
| `AltMovieDbConfig` | 自定义 TMDB 地址（反代/镜像），同时改图片下载出口 |
| `DefaultTmdbLibrary` | 新建媒体库时把 TheMovieDb 刮削器置顶 |
| `LocalTmdbCache` | 本地 TMDB 响应缓存（v2，落盘 + 用户 override + 合并补丁） |
| `ChineseMovieDb` / `ChineseTvdb` | 中文化元数据源 |
| `PreferTraditionalChinese` | 回填前把简中概要转繁中 |
| `ClearTagline` | 回填前清空 Taglines，去掉 TMDB 英文标语噪音 |
| `EnhanceMovieDbPerson` | 演员信息增强 |
| `EnhanceNfoMetadata` | NFO 元数据增强 |
| `MovieDbEpisodeGroup` | TMDB 剧集组（分集顺序映射） |
| `OptimizeMovieDbEpisodeScraping` | 按季批量抓分集，减少 TMDB 请求 |
| `PreferOriginalPoster` | 优先原版海报 |
| `BeautifyMissingMetadata` | 缺元数据的分集标题美化 |
| `EnhanceMissingEpisodes` | 缺集视图增强 |
| `OptimizeSubtitle` | strm 条目的 OpenSubtitles 搜索优化（非 File 协议时补 hash） |
| `DolbyVisionProfile` | 双层杜比 Vision Profile 7 标注 |
| `HdrVividTagging` | HDR Vivid 探测 → 落库 `Comment` → 覆写显示 |

### 媒体信息与 ISO
| 类 | 作用 |
|---|---|
| `ExclusiveExtract` | 刷新期间的独占控制（串行化提取，避免并发探测同一文件） |
| `IsoExtraction` | ISO 提取（跨平台，UDF 解析 / 挂载），见 [iso.md](iso.md) |
| `EnableImageCapture` | 截图（含 ISO / 远程） |
| `EnableDeepDelete` | 深度删除（连文件一起删） |

### 库与条目显示
| 类 | 作用 |
|---|---|
| `MergeMultiVersion` | 多版本合并（`PresentationUniqueKey` 归并） |
| `NoBoxsetsAutoCreation` | 不自动创建合集库 |
| `ShowCollectionsFromSeasons` | 剧集页合集区块同时考虑季的合集归属 |
| `HidePersonNoImage` | 按偏好隐藏无头像的演职人员 |

### 片头片尾与通知
| 类 | 作用 |
|---|---|
| `UnlockIntroSkip` | 解锁 Emby 原生片头探测（Premiere 功能） |
| `EnhanceNotificationSystem` | **接管系统新入库通知**：`ConvertToGroups` 收集本批集 + `SendNotification` / `QueueNotification` 拦截 + 删除抑制。只有一个开关，没有独立子开关 |
| `ChapterChangeTracker` | 章节变更追踪（配合 ISO / 元数据回填，非独立功能） |

## 已知陷阱

1. **返回 `Task<T>` 的方法，Postfix 的 `__result` 是 Task 本身**（不自动解包）。
   要注入数据必须**换包**：构造新 Task 返回。`GetMediaInfoInternal` 就是这种情况。
2. **一行转发的小方法会被 JIT 内联，补丁无效**。典型：`BaseSqliteRepository.CreateConnection`。
   必须往上一层找大方法体（见 [chinese-search.md](chinese-search.md)）。
3. **写路径的 Postfix 持有信号量和静态锁**。在那里做任何耗时操作（比如重建索引）= 全库冻结。
4. **`__instance` 未必是 `GetItemById` 返回的那个实例**。传 `skipHasMediaInfoCheck` 这类快路径时，
   要确认调用方已经对同一个对象做过前置检查。
5. **DiscUtils 的 `searchPattern` 用 `"*"` 不匹配带扩展名的文件**，必须 `"*.*"` 或全量列举。
   这个坑让 `UdfTitleResolver.Resolve(path)` 长期返回 null（Windows 被挂载路径掩盖，看不出来）。

## 排查

- 启动日志 `All N patches initialized successfully` —— N 应该是 37。
- `PatchManager.IsModSuccess()` 为 false 时，设置页会显示状态条（`[VisibleCondition(nameof(IsModSuccess), IsFalse)]`）。
- 详细报告：`PatchManager.GetDiagnosticReport()`（Debug 级，`embyserver.txt` 里看不到，需要开 Debug 或临时改 Info）。
- 单个补丁不生效时，先确认 `PatchTracker.Status` 与 `ErrorMessages`，再看是不是 `IsSupported = false` 被版本判断关掉了。
