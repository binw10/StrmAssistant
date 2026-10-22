# 0002 · 用 sqlite3_load_extension，不用 sqlite3_auto_extension

- **状态**：✅ 已采纳（实机验证）
- **日期**：2026-10-20
- **相关**：[../architecture/chinese-search.md](../architecture/chinese-search.md)

## 背景

中文搜索需要给 Emby 的 SQLite 加一个自定义分词器（`libsimple`）。加载原生扩展的常规做法是
`sqlite3_auto_extension()` —— 注册一次，之后所有新建连接自动带上。

## 决策

**放弃 `sqlite3_auto_extension`，改为在 `PooledDatabaseConnectionManager.CreateConnection` 的
Postfix 里按句柄调用 `sqlite3_load_extension`。**

## 理由

日志实证：**Emby 的写句柄在插件加载之前就已经建好了**。`auto_extension` 只对调用之后新建的连接生效，
对已存在的连接完全无效 —— 而我们需要打的那个连接恰恰是最早建的。

## 后果

- 必须找到不会被内联的补丁点。`BaseSqliteRepository.CreateConnection` 是一行转发、会被 JIT 内联，
  打上去覆盖不到写路径；真正有效的是 `Emby.Sqlite.PooledDatabaseConnectionManager.CreateConnection(bool, ct)`
  （`public override`、方法体大、私有字段 `Id` 就是仓库名）。
- 必须处理"多个连接共享同一原生句柄"的问题：`SqliteConnectionInfo.Clone` 会
  `new SQLiteDatabaseConnection(db, ...)` 复用同一个 `db`。所以按**句柄**注册，
  用 `ConditionalWeakTable` 去重，一个池位只加载一次。

## 其它项目的做法（都是同一结论）

| 项目 | 做法 |
|---|---|
| 本仓 | 按句柄 `sqlite3_load_extension` |
| **wt** | 同样 patch 仓库 `CreateConnection`（同缺陷）；其生态的终极解是直接替换 system 内的 `sqlite3.dll` |
| **PRO .56** | 完全不 patch，自己 `CreateConnection(false)` + `sqlite3_db_config(db, 1005, 1)` + `sqlite3_load_extension` |

**三家都没有用 `auto_extension`。**
