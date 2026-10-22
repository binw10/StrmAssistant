# 优化方案 v3 · 分词器写路径 + HTTP ISO

> ⚠️ **P0 章节已被 `优化方案-v4-实测版.md` 取代**（2026-10-20 实机验证：v3 的推断被证实，并实测出第二个 bug——索引重建跑在只读句柄上；P0 已落地并验证通过）。
> P1 章节的设计在 v4 §4.2 中进一步细化。本文件保留作推演过程追溯。

> **项目**：`StrmAssistant-myp015` @ `1e90c70`（本地领先 `origin/myp015` 2 个提交，未推送）
> **PRO 基准**：`3.0.0.56`（与 `.55` 功能面零变化，已实证）
> **日期**：2026-10-20
> **取代**：`优化方案-汇总-v2.md` 的 P0 / P1 章节（v2 的 P2~P4 排序仍然有效，见 §4）
> **性质**：**方案，未改一行代码**。等确认后再动手。

---

## 〇、本次复核做了什么

对 v2 的每一条结论回代码/反编译/日志做二次验证。结论：**v2 的方向对，但 P0 的修法不完整，P1 的路线可以砍掉一半工作量。**

| v2 结论 | 复核结果 |
|---|---|
| P0 根因是 JIT 内联绕过 detour | ✅ 成立，且找到了**更直接的证据**（写连接句柄在插件加载前就建好了） |
| P0 修法 = 改 patch `PooledDatabaseConnectionManager.CreateConnection` | ⚠️ **必要但不充分**。只换 patch 目标会把一个"持锁全库冻结"的隐患变成必现 |
| `sqlite3_auto_extension` 不可行 | ✅ 成立，理由从"推测"升级为"日志实证" |
| P1 = 分片 Range + **自写 UDF extent 解析** | ❌ **过度设计**。复用现有 `DiscUtils.Udf` + 一个 Range 流即可，代码量约为原方案的 1/3 |
| P2~P4 | 排序合理，仅 P3 内部顺序微调 |

---

## 一、P0：分词器写路径（唯一必修）

### 1.1 症状（已复现，不是推测）

```
11:07:29.123 Error SqliteItemRepository: Error saving items
  SQLitePCL.pretty.SQLiteException: Error: no such tokenizer: simple
     at SqliteItemRepository.SaveSearchRows(...)
     at SqliteItemRepository.SaveItemsInTranscation(IDatabaseConnection db, ...)
     at SqliteItemRepository.SaveItems(...)
11:07:29.174 Error ProviderManager: Error refreshing item 原盘 428 ...strm
```

影响面不是"搜索坏了"，而是 **`SaveItems` 事务整体回滚 → 所有元数据写入失败**（刷新、刮削、打标签全挂）。

### 1.2 证据链（四条，全部可复核）

**① 写连接句柄在插件加载之前就建好了** —— `programdata/logs/embyserver.txt`：

```
11:09:21.615 SqliteItemRepository: Opening sqlite connection to ...\library.db. isReadOnly: False   ← 写句柄
11:09:22.011 SqliteItemRepository: Opening sqlite connection to ...\library.db. isReadOnly: True    ← 读句柄
11:10:43.044 StrmAssistantPlus: EnhanceChineseSearch - Current tokenizer (before) is simple          ← 插件才开始 patch
```

→ **`sqlite3_auto_extension` 注册在插件 init，对这两个已存在的句柄完全无效**。v2 排除它的结论正确，现在有实证。

**② 连接克隆共享同一个原生句柄** —— `SQLitePCL.pretty.SqliteConnectionInfo.Clone`（反编译）：

```csharp
public IDatabaseConnection Clone(bool closeOnDispose) {
    IDatabaseConnection c = Connection;
    if (c == null) c = (Connection = ConnectionFactory(IsReadOnly));  // 父连接懒创建
    IDatabaseConnection clone = c.Clone(closeOnDispose);              // → new SQLiteDatabaseConnection(db, ...) 同一个 db
    ((SQLiteDatabaseConnection)clone).ParentConnection = this;
    return clone;
}
```

→ 扩展是**按原生句柄**注册的。**只要在一个克隆上加载成功，该池位的父句柄 + 后续所有克隆永久可用。**
→ 所以"每连接加载一次"其实是"每池位加载一次"，去重成本几乎为零（见 §1.4-③）。

**③ 所有连接的唯一出口是一个大方法，且不可内联** —— `Emby.Sqlite.PooledDatabaseConnectionManager`：

```csharp
public override IDatabaseConnection CreateConnection(bool isReadOnly, CancellationToken ct) {
    CheckDisposed();
    if (!isReadOnly || _maxReadConnections == 0) { var p = WaitForWriteAccess(ct); return Clone(p, OnWriteConnectionClosed, ct); }
    var p2 = _readConnections.Take(ct); return Clone(p2, OnClosed, ct);
}
```

构造于 `BaseSqliteRepository`：`new PooledDatabaseConnectionManager(CreateNewConnection, Logger, max, GetType().Name)` → 私有字段 `Id` = **仓库类型名**（`"SqliteItemRepository"`）。

**④ 写路径的调用被内联绕过** —— `BaseSqliteRepository` 的两个前置重载都是一行转发：

```csharp
protected IDatabaseConnection CreateConnection()              => CreateConnection(isReadOnly: false);
protected IDatabaseConnection CreateConnection(bool ro)       => CreateConnection(ro, CancellationToken.None);
protected IDatabaseConnection CreateConnection(bool ro, CancellationToken ct) => ConnectionManager.CreateConnection(ro, ct);
```

`SaveItems`（`/tmp/sir.cs:1268`）用的是**无参**那个 → 两层一行转发被 JIT 内联 → 直接落到 `ConnectionManager.CreateConnection`，**被 patch 的 3 参方法根本不在调用路径上**。这解释了 `[TokDbg]` 实测"读路径有日志、写路径零日志"。

### 1.3 v2 修法的三处补强（本次新增）

v2 只说了"换成 patch 管理器"，但直接换会踩两个坑：

#### 补强 A：`PatchPhase2` 必须搬出 postfix（否则会把隐患变必现）

现状 `CreateConnectionPostfix` 是**在 `_lock` 里内联跑 `PatchPhase2`**，而 Phase2 可能执行 `RebuildFts`（整表 `insert into fts_search9 ... select ... from MediaItems`）。

换成管理器 patch 后，**第一次命中很可能就是写连接**（写连接每次 `SaveItems` 都新建克隆）。届时：

- 线程持有 `_writeConnectionPool` 信号量（`WaitForWriteAccess` 已把所有读连接 `Take` 干净）
- 同时持有静态 `_lock`
- 然后在里面跑一次全量 FTS 重建 → **整个 Emby 的数据库访问冻结到重建结束**；若重建过程中有任何代码再取连接 → **直接死锁**

→ 必须拆成：postfix 只做"加载分词器 + 一次性 `PatchSearchFunctions()`"，**重建改为后台任务 + 自有连接**。

#### 补强 B：`RebuildFts` 可能一直跑在只读句柄上（潜在必然失败）

`CreateNewConnection(bool isReadOnly)` 里只读分支是 `ConnectionFlags.ReadOnly`（`sqlite3_open_v2` 带 READONLY）。而 Phase2 传进去的连接，恰恰是**第一个触发 postfix 的连接** —— 在现在的实现里通常是读连接。`RebuildFts` 在只读句柄上必然抛 `attempt to write a readonly database`。

目前日志里看不到这个失败，只是因为**索引一直处于同步状态、从没触发过重建**。一旦触发（norm v2 升级 / 分词器二进制变更 / 用户手动排程重建），很可能直接失败并陷入"每次重启都重建一次、每次都失败"。

→ 重建必须走**自己开的读写连接**（`OptimizeSearchIndex` 已有现成写法可抄：`SQLite3.Open(path, ReadWrite)` + `PRAGMA busy_timeout` + `LoadTokenizerExtension`）。

#### 补强 C：探针改为句柄级去重（顺带性能收益）

`HasTokenizerOnConnection` 每次建连都执行一次 `SELECT simple_query(...)`。Emby 每次查询/写入都建连 → **每次 DB 操作多一次 SQL 往返**。既然句柄是共享的（证据②），改成"按原生句柄对象身份记 HashSet"即可，热路径零开销。

### 1.4 改动清单

| # | 落点 | 改动 | 量级 |
|---|---|---|---|
| ① | `Mod/EnhanceChineseSearch.cs` | `OnInitialize` 增加反射：`Emby.Sqlite.dll` → `PooledDatabaseConnectionManager` → `GetMethod("CreateConnection", [bool, CancellationToken])`；失败不致命（回退旧 patch） | ~15 行 |
| ② | 同上 `PatchPhase1` | 改为**两个 patch 都打**：`BaseSqliteRepository.CreateConnection`（保留，负责拿到 `DbFilePath`）+ `PooledDatabaseConnectionManager.CreateConnection`（新增，负责覆盖全部路径）。任一失败只降级不抛 | ~20 行 |
| ③ | 同上 `CreateConnectionPostfix` | 新增 `HashSet<object>`（`ReferenceEqualityComparer`，键 = `sqlite3_db` 反射出的句柄对象）；已存在则直接返回。`__instance` 分支：仓库 → 走原 `DbFilePath` 逻辑；管理器 → 读私有 `Id` 字段（仅用于日志/Phase2 判定） | ~35 行 |
| ④ | 同上 新方法 `RunPhase2InBackground` | 首次加载成功后 `Task.Run`（延迟 3s，避开启动高峰）：`ResolveLibraryDbPath()` → `SQLite3.Open(ReadWrite)` → `busy_timeout` → `LoadTokenizerExtension` → 现有状态机（含 `RebuildFts`）→ 释放。原 Phase2 的"状态判定 + 重建"整体搬进来 | ~40 行 |
| ⑤ | 同上 `PatchPhase2` 拆分 | `PatchSearchFunctions()` 留在 postfix（Harmony patch，不碰 DB、便宜）；其余搬 ④ | ~20 行 |
| ⑥ | 同上 日志 | 首次加载 / 重建 / 失败各打一条 Info（现在很多分支只有 Debug，出问题查不到） | ~10 行 |

合计约 **140 行**，全在 `EnhanceChineseSearch.cs` 一个文件内，无新增依赖。

### 1.5 验收（四条，缺一不可）

1. 启动后 `EnhanceChineseSearch - Load Success (search patches active)`，且新增一条"tokenizer loaded on handle #…（id=SqliteItemRepository）"
2. `POST /emby/Items/428/Refresh?metadataRefreshMode=FullRefresh` → **无** `Error saving items`；连刷 5 个条目，`no such tokenizer` 计数保持 **0**
3. 搜索「杜比」仍命中 item 424（不回归）
4. **重建路径**：删掉 `strmassistant_chinese_search_norm2.done` + `..._tokenizer.done` → 重启 → 日志出现 `Rebuild completed`（**验证补强 B**），且重启后索引仍可用

> 第 4 条是本次新增的关键验收：不改就永远测不到"重建跑在只读句柄上"这个雷。

### 1.6 回退

两个 patch 独立打；管理器 patch 失败时自动退回现状（仓库 patch），行为与今天一致。整体可通过选项关闭，无需回滚代码。

---

## 二、P0.5（可选）：升级时"关选项"改为"排程还原"

**现状**（`Plugin.cs:858-880`）：版本升级时把 `EnhanceChineseSearch` 置 `false`，但**不动 `fts_search9`**。结果是一个半吊状态：

- 库里索引仍是 `tokenize="simple"`（没有分词器就打不开）
- 选项已关闭，所以 `PatchPhase2` 两个分支都不进 → 永远不会还原成 `unicode61`
- 唯一撑着不炸的，就是"分词器仍然无条件加载"（PR #1 的改动）

**建议**：升级关闭时，同时写 `EnhanceChineseSearchRestore = true`（或落一个 `restore.pending` 标记），让下次启动**真正还原成 Emby 默认分词器**，从此不依赖扩展。

**代价**：升级后首次重启会跑一次全量索引重建（大库几分钟）。**收益**：彻底消灭"配置关闭 + 索引依赖扩展"这个最脆的组合。

> 这条要不要做，取决于你更在意"升级后首次启动变慢"还是"少一个隐患"。我倾向做，但放在 P0 之后单独验证。

---

## 三、P1：HTTP / strm ISO（替换 v2 的 ISO-1'~4'）

### 3.1 现状盘点（代码事实）

| 能力 | 本地 `.iso` | 远程 `.iso.strm` |
|---|---|---|
| 探测（Container/时长/流） | ✅ 挂载后 ffprobe | ✅ 8MB 头部窗口 → 临时文件 → ffprobe（`GetRemoteProbePath`，已验证可用） |
| 章节（MPLS PlayListMark） | ✅ `UdfTitleResolver.Resolve(path)` | ❌ 窗口内没有 `MPLS0300`，`DiscUtils` 构造即抛 `Substream extends beyond end of parent stream` |
| 截图 | ✅ | ❌ 帧数据不在窗口内 |

### 3.2 v2 方案的问题

v2 写的是"分片 Range 下载 + **UDF extent 解析**"。但项目里已经有 `DiscUtils.Udf`（0.16.13，已 ILRepack 进插件），**再手写一遍 UDF extent 解析属于重复造轮子**，而且是历史上最容易出 bug 的地方（`UdfTitleResolver` 805 行，上次就是漏读保留字节导致章节全空）。

### 3.3 优化后的方案：Range 流 + 复用 DiscUtils

核心洞察：`DiscUtils` 抛异常的原因不是"不支持流"，而是**流的 `Length` 只有 8MB**（分区声明长度 > 流长度 → 拒绝构造）。给它一个 **`Length` 正确、按需 Range 取数的可 Seek 流**，它就能正常遍历 UDF，把 MPLS 目录项找出来。

| 步 | 内容 | 落点 | 量级 |
|---|---|---|---|
| **ISO-1** | 新增 `Common/HttpRangeStream.cs`：`Stream` 子类，`CanSeek=true`；`Length` 取 `Content-Range` 总长（HEAD 一次）；`Read` 按 1MB 块懒取 + LRU 缓存（复用 `LruCache.cs`）；复用 `DownloadRange` 的空闲看门狗思路 | 新增文件 | ~200 行 |
| **ISO-2** | `UdfTitleResolver` 增加 `Resolve(Stream, string label)` 重载（内部 `Resolve(IDiscFs)` 已存在，只需换入口） | `Common/UdfTitleResolver.cs` | ~25 行 |
| **ISO-3** | `IsoExtraction` 远程分支：探测仍走 8MB 窗口（已验证可用，不动）；**章节**改用 `HttpRangeStream` + `Resolve(stream)` → `BuildChapterState`；解析失败静默回退（保持现状） | `Mod/IsoExtraction.cs` | ~35 行 |
| **ISO-4** | 缓存 + 清理：`HttpRangeStream` 的块缓存按 URL 复用（TTL 同 `RemoteCacheTtl` 30 分钟），避免每次刷新都重拉 | `Common/IsoMounter.cs` | ~20 行 |
| **ISO-5** | 选项文案 + 三语 resx（说明"远程 ISO 支持章节，不支持截图"） | `Options/IsoToolOptions.cs` + `Resources*` | ~15 行 |

**截图明确不做**：远程 ISO 抽帧需要 ffmpeg 按时间轴随机读 m2ts，用 Range 流喂不了 ffmpeg（它要 URL 或本地文件），做"稀疏本地缓存文件"属于另一个量级的工程。回退到现有海报，并在选项里写清楚。

### 3.4 红线与验收

**红线**：① 不改 `MediaSourceInfo.Path`；② Range 不支持 / 超时 → 静默回退现状（不能因为章节把探测搞挂）；③ 零新私有依赖（只用 `System.Net.Http` + 现有 DiscUtils）。

**验收**：样本 `原盘\杜比视界测试：双层 FEL.iso.strm`（item 428）刷新后
1. `Chapters3` 有章节记录（当前为 0）
2. `Container` / `RunTimeTicks` / 流信息不回归（与现在一致）
3. 远程不可达时，刷新仍成功、无章节（回退路径）

---

## 四、P2 / P3 / P4 重排

排序原则：**先修"会静默毁数据"的，再修"每次都多花钱"的，最后修"看着不爽"的**。

| 序 | 项 | 为什么这个位置 | 量级 |
|---|---|---|---|
| 1 | **P0 分词器**（§1） | 唯一会让元数据写入整体失败的 bug | 小 |
| 2 | **P0.5 升级排程还原**（§2，可选） | 消灭最脆的配置组合 | 小 |
| 3 | **P2 测试项目**（只测 5 个纯算法模块） | 后面 P1/P3 都要动"历史 bug 区"，先有网再走钢丝 | 中 |
| 4 | **P1 HTTP ISO**（§3） | 唯一大工作量，但有明确回退路径，可独立上线 | 大 |
| 5 | **P2 筛选菜单 `HasDoubanId`/`HasImdbId`** | 纯增量功能，不影响存量 | 中 |
| 6 | **P3 `catch` 分级 + `Resources.Designer.cs` 自动生成** | 工程卫生，穿插做 | 小 |
| 7 | **P3 `DoubanAssist` 拆分** | 迭代最频繁，但**必须在测试项目之后** | 中 |
| 8 | **P4 G4 → G2 → G3** | G3 动 `PresentationUniqueKey`，高风险，最后考虑 | 小/小/高 |

**P2 测试项目的 5 个模块**（不变）：`UdfTitleResolver`（805 行，历史 bug 区）、`DolbyVisionProfile`、`LanguageUtility`、`MultiVersionService` 的档位标签（前端 `scoreOf` 依赖档位词在首位）、`Options/Store/*Migration`。

---

## 五、明确不做（复核后维持）

| 项 | 理由 |
|---|---|
| `sqlite3_auto_extension` | 写句柄 11:09:21.615 就建好了，插件 11:10:43 才加载 → 覆盖不到（实证） |
| 自写 UDF extent 解析 | 已有 `DiscUtils.Udf`，重写只增加 bug 面（§3.2） |
| 全量 DI / Autofac | 生命周期与 Emby 冲突 |
| 拆 `LibraryApi` / `EnhanceChineseSearch` | 内聚度高 |
| 宽 `catch` 改具体异常 | 单条目失败会拖垮整批 |
| 追测试覆盖率 | Emby 类型 mock 成本极高 |
| 远程 ISO 截图 | ffmpeg 喂不了 Range 流，收益/成本不匹配 |

---

## 六、执行顺序（每步独立可验收、可回退）

```
第 1 步  P0 分词器
         ① patch 管理器（只加，不删旧的）→ 编译 → 实机验 §1.5-1/2/3
         ② Phase2 搬后台 + 只读句柄修正 → 验 §1.5-4（删标记重启）
         ③ 句柄去重（性能）→ 观察日志无重复加载

第 2 步  P0.5（可选，看你对"升级后首次启动变慢"的接受度）

第 3 步  P2 测试项目（5 个纯算法模块）

第 4 步  P1 HTTP ISO（ISO-1 → ISO-2/3 → 缓存 → 文案，逐步验）

第 5 步  P2 筛选菜单 / P3 卫生项 / P4
```

---

## 七、需要你拍板的三件事

1. **P0 是否按 §1.4 全量做**（含把 Phase2 搬到后台）？还是先只做"加管理器 patch"这一半，验完再动 Phase2？
   → 我建议**全量做**：只做一半的话，写路径首次命中就会在锁里跑重建，风险比现在更高。

2. **P0.5（升级时排程还原）要不要做**？
   → 做的话，升级后首次重启会有一次全量索引重建（你库里有 4 个库、条目不多，应该很快）。

3. **P1 的截图能力确认放弃**？只做"远程 ISO 章节"。
   → 若一定要截图，需要另立"远程 ISO 稀疏缓存文件"子项目，工作量翻倍。

确认后我从第 1 步开始，每步实机验证 + 记录，不一次性堆完再测。
