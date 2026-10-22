# 自定义端点

除 Emby 原生 API 外，插件自己暴露 13 个端点，全部在 `Web/Api/`，处理逻辑在 `Web/Service/ItemService.cs`。

> 为什么文档里没有单独的 `api/` 目录：我们对外的自定义端点就这么几个，其余全是 Emby 自己的，
> 单独建目录撑不起来。合并在这里。

## 菜单类（前端右键菜单调用）

| 方法 | 路径 | 入参 | 作用 |
|---|---|---|---|
| POST | `/StrmAssistant/Items/ExtractMediaInfo` | `{ "Ids": ["<itemId>"] }` | 提取媒体信息（走 `MasterSemaphore`，不经过追更队列） |
| POST | `/StrmAssistant/Items/DeleteMediaInfo` | `{ "Ids": [...] }` | 删除媒体信息备份 |
| POST | `/Items/{Id}/ClearMediaInfo` | — | 清空条目的媒体信息 |
| POST | `/Items/{Id}/ClearChapterImages` | — | 清空章节图 |
| POST | `/Items/{Id}/ClearIntro` | — | 清空片头片尾标记 |
| POST | `/Items/{ItemId}/Lock` | — | 锁定/解锁条目元数据 |
| POST | `/Items/{Id}/DeleteVersion` | — | 删除某个版本 |

## 查询类

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/MultiVersion/Info?Id=<itemId>` | 多版本信息，返回 `Sources[].Label/Hdr/Bitrate/Size`。**`[Unauthenticated]`，且没有插件前缀** |
| GET | `/Douban/Ratings` | 豆瓣评分（隐藏） |

## 静态资源注入（前端脚本）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/{Web}/modules/shortcuts.js` | 右键菜单注入 |
| GET | `/{Web}/components/strmassistant/strmassistant.js` | 主前端脚本（多版本排序、徽章、日志反转等） |
| GET | `/{Web}/modules/mediainfo/mediainfo.js` | 媒体信息展示注入 |

这三个不是真文件，是**动态生成的 JS**：`Web/Helper/ShortcutMenuHelper.cs` 读模板，
把插件选项（`SHOW_MEDIA_INFO`、`REVERSE_LOGS`、`SHOW_LIB_COUNT` 等）作为常量替换进去，
再附上脚本版本号。所以**改完选项不需要重启**，前端刷新即生效。

## 其它

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/Library/VirtualFolders/Copy` | 复制媒体库配置 |

## 调用约定

- **鉴权**：除 `/MultiVersion/Info` 标了 `[Unauthenticated]`，其余都要求管理员角色（`[Authenticated(Roles = "Admin")]`）。
- **API Key 两种传法行为不同**：
  - `?api_key=xxx` —— 对**普通 Emby API** 可用；
  - `/emby/UI/View` 这类 **GenericEdit 页面**必须用 `X-Emby-Token` 头，用 `api_key=` 会 400（`Parameter 'user'`）。
- **API Key 会失效**（Emby 重启或用户登出后重建）。取法：
  `programdata/data/authentication.db` → 表 `Tokens_2` → 按 `DateCreatedInt desc` 取最新，
  注意 `UserId` 是 **int 不是 Guid**。
- 调试端点行为时优先用 `curl --noproxy '*'`，本机代理会拦 `127.0.0.1`。
