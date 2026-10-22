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

## 增量维护

- **`ItemAdded` 事件** → `_pendingFtsRefresh` 队列 → 后台批量刷新。
- **启动 catch-up**：补齐服务器关闭期间外部工具入库的条目。
- **智能启动**：tokenizer 已是目标值时**跳过全量重建**（`RebuildFts` 走事务保证原子性，
  tokenizer 匹配即说明上次已完整提交）。避免 O(N) 全表扫描阻塞启动。
- 后续新增/更新条目由事件 + 增量队列覆盖。

## 当前参数（改动前先看这里）

| 项 | 值 | 说明 |
|---|---|---|
| `busy_timeout` | 5000 ms | 与 Emby 写连接争锁时的等待上限 |
| 事务模式 | `BeginTransaction(Deferred)` | 惰性拿写锁 |
| 扫描期让路 | **无** | 大库扫库期间 catch-up 仍会抢 `library.db` |
| 自适应调度 | **无** | 刷新间隔固定 |
| 拼音召回 | **无** | 不支持 chong/zhong 多音字互搜 |

> 上游 OneManJS 的版本在这四点上都做了优化（`busy_timeout=1000`、批量路径 `BEGIN IMMEDIATE`、
> 扫描期跳过 catch-up、空闲 3s / 积压 15s / 扫描中 60s 自适应）。我们的索引架构是自己重写的，
> 只能取调度策略，不能整段合并。评估见 `history/2026-10/` 相关记录。

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
