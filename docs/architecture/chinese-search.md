# 中文搜索链路

Emby 的 FTS5 索引默认用 `unicode61` 分词器，中文被整段当成一个 token，搜"神医"匹配不到"神医助手"。
本插件把索引换成 `simple` 分词器（自研 `libsimple`，支持中文切分），并在条目入库时维护增量索引。

实现集中在 `Mod/EnhanceChineseSearch.cs`（约 1700 行）+ `Tokenizer/`（原生库）。

## 原生库

`Tokenizer/` 下是 `libsimple` 的源码与三平台预编译产物：

```
Tokenizer/
├── libsimple-src/      源码
├── build-libsimple.sh  构建脚本
├── win/                sqlite3 扩展 dll
├── linux/
└── linux-arm64/
```

README 里有编译说明。**换 sqlite 版本或平台时必须重新编译**，否则 `sqlite3_load_extension` 会失败。

## 打补丁的位置（关键）

Emby 的连接创建有两层：

```
BaseSqliteRepository.CreateConnection()      ← 一行转发，会被 JIT 内联，打不上
        ↓
Emby.Sqlite.PooledDatabaseConnectionManager.CreateConnection(bool, ct)   ← 真正的落点
```

唯一可靠目标是后者：`public override`、方法体大（不可内联）、私有字段 `Id` 就是仓库类型名
（`library.db` / `authentication.db` …）。

**为什么不用 `sqlite3_auto_extension`**：写句柄在插件加载**之前**就已经建好了，auto_extension
只对之后新建的连接生效，等于对已存在的连接无效。三家实现（本仓、wt、PRO）最终都没走这条路。
详见 [../decisions/0002-用-sqlite-扩展而非-auto-extension.md](../decisions/0002-用-sqlite-扩展而非-auto-extension.md)。

## 句柄注册

`SqliteConnectionInfo.Clone` 会 `new SQLiteDatabaseConnection(db, ...)` 复用**同一个原生句柄**，
父连接是懒创建的。所以扩展要**按句柄注册**，用 `ConditionalWeakTable` 去重 —— 一个池位只需加载一次。

## 索引重建的两阶段

```
Phase 1（在补丁 Postfix 里）
  DROP / CREATE FTS 表（tokenize='simple'）
  只做结构变更，快

Phase 2（必须搬出 Postfix，后台 Task）
  INSERT 全量数据
  用仓库的【写连接】CreateConnection(false, ct)
```

🔴 **两个必须遵守的约束**：

1. **重建不能跑在只读句柄上**。首个触发 Postfix 的永远是读连接（`ConnectionFlags.ReadOnly`），
   在它上面 `DROP/CREATE/INSERT` 必失败。
2. **Phase 2 绝不能留在 Postfix 里**。写路径的 Postfix 持有 `_writeConnectionPool` 信号量 + 静态 `_lock`，
   就地重建 = 全库冻结，甚至死锁。实测搬到后台 Task + 写连接后，重建耗时 0.45~2.1s。

## 索引怎么保持同步

**不需要插件维护增量**：重建时把 Emby 自己用的那张 FTS 表（`fts_search9`）用新分词器
重建一次，之后 Emby 的条目写入路径照常往里写，索引自然是最新的。

所以插件只在两种时机做全量重建：

- 首次启用中文搜索；
- 版本/归一化方案变更（靠标记文件判断，见下）。

> 上游 OneManJS 的分支另有一套增量 catch-up 循环（`_pendingFtsRefresh` + 定期刷新 +
> 扫描期让路），那是它自己的架构，本仓不需要，也不要去搬。

## 标记文件

三个标记放在 `programdata/plugins/configurations/`：

| 文件 | 作用 |
|---|---|
| `strmassistant_chinese_search_norm2.done` | 归一化方案 v2 已应用（删掉可复现重建） |
| `strmassistant_chinese_search_tokenizer.done` | 记录分词器文件的 SHA1 |
| `strmassistant_chinese_search_rebuild.pending` | 有待执行的重建 |

**智能跳过**：若当前 tokenizer 已是目标值、且标记齐全，启动时跳过重建 ——
重建走事务保证原子性，tokenizer 匹配即说明上次已完整提交，不必再全表扫一遍。

## 当前参数（改动前先看这里）

| 项 | 值 | 说明 |
|---|---|---|
| `busy_timeout` | 5000 ms | 与 Emby 写连接争锁时的等待上限 |
| 事务模式 | **`BEGIN IMMEDIATE`** | 重建是纯写事务，在 BEGIN 处直接抢写锁；拿不到立即失败交由上层重试 |
| 增量刷新 | **无** | FTS 表由 Emby 自己的写入路径维护，插件只在启用/版本变更时做一次性全量重建 |
| 扫描期让路 | **无** | 没有增量循环，无从挂载 |
| 拼音召回 | **无** | 不支持 chong/zhong 多音字互搜 |

### 为什么事务要用 IMMEDIATE 而不是 Deferred

重建的第一条语句就是 `DROP TABLE`，是纯写事务。`Deferred` 会以**读事务**开局，
在首次写入时才升级成写锁；WAL 模式下如果这期间别的连接提交过，
升级会返回 `SQLITE_BUSY_SNAPSHOT` —— 这个错误**不受 `busy_timeout` 约束**，
等多久都没用，只能整个事务重来。`IMMEDIATE` 在 `BEGIN` 处就把写锁抢下来，
拿不到立刻失败，交给重试逻辑处理。

### 上游那套参数为什么不照抄

上游 OneManJS 的分支有**增量 catch-up 循环**（`_pendingFtsRefresh` 队列 + 定期刷新），
所以它会：`busy_timeout` 降到 1000ms 及时让路、扫描期跳过 catch-up、
刷新间隔自适应（空闲 3s / 积压 15s / 扫描中 60s）。

本仓没有这个循环 —— 我们只是把 Emby 用的 FTS 表重建一次换成 `simple` 分词器，
之后由 Emby 自身的写入维护。所以：

- 降 `busy_timeout` 只会让**一次性重建**更容易失败，是回退；
- 「扫描期跳过」「自适应间隔」没有挂载点。

评估过程见 `history/2026-10/` 与提交 `2b88024`。

## 关闭与还原

关闭「中文搜索」时，**同一次启动内**把索引还原成 `unicode61`，不留下一个 Emby 不认识的 tokenizer
（否则原生搜索会报错）。

## 验证手法

```bash
# 复现重建路径：删完成标记
rm programdata/plugins/configurations/strmassistant_chinese_search_norm2.done

# 模拟版本升级：改标记文件（格式 <方案号>|<版本>）
# 内容写旧版本号即可触发升级分支
programdata/plugins/configurations/StrmAssistantPlus_chinese_search_last_seen_version.txt
```

日志关键字：`EnhanceChineseSearch - ` 开头的行（重建开始/完成/耗时/还原）。

⚠️ `Logger.Debug` 在 `embyserver.txt` 里看不到，诊断埋点要用 `Info`。
