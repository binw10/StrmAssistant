# 文档索引

Emby 神医助手 Plus（StrmAssistantPlus）的文档库。插件本体是 Emby 4.9/4.10 的 Harmony 补丁集，
重点解决 strm 直链播放、中文搜索、ISO 原盘、多版本合并、豆瓣中文化这几类问题。

## 目录结构

| 目录 | 放什么 | 状态 |
|---|---|---|
| [`architecture/`](architecture/overview.md) | **当前**架构：模块划分、补丁模型、关键链路 | 持续更新 |
| [`decisions/`](decisions/) | ADR：某个选择**当时为什么这么定**，一决策一页 | 只追加，不改写 |
| [`dev/`](dev/build-and-deploy.md) | 开发者手册：构建、测试、实机验证、选项约定 | 持续更新 |
| [`history/`](history/index.md) | 按年月归档的方案 / 实施记录 / 回归测试 | 只追加 |
| [`assets/`](assets/) | 截图、流程图等二进制素材 | — |

> `history/` 不是垃圾桶。这个项目的大量结论来自**实机验证**（哪次挂载成功、`Chapters3` 是几、
> 哪个 Emby 版本返回 500），证据链都留在那里。遇到"为什么当初没做 X"，先去 `history/` 和
> `decisions/` 翻，不要重新推导。

## 我想做 X，该读哪篇

| 我要做的事 | 读这篇 |
|---|---|
| 本地编译、部署到自己的 Emby | [dev/build-and-deploy.md](dev/build-and-deploy.md) |
| 跑单元测试 | [dev/testing.md](dev/testing.md) |
| 在真机上验证一个改动（怎么取 token、怎么触发任务、看哪个日志） | [dev/verify-on-emby.md](dev/verify-on-emby.md) |
| 加一个选项 / 改一个选项的单位 | [dev/options-conventions.md](dev/options-conventions.md) |
| 搞清补丁是怎么打上去的、现在有多少个 | [architecture/patching.md](architecture/patching.md) |
| 改中文搜索（分词器 / FTS 索引） | [architecture/chinese-search.md](architecture/chinese-search.md) |
| 改 ISO 相关（挂载、UDF、远程 Range） | [architecture/iso.md](architecture/iso.md) |
| 适配新的 Emby 版本 | [architecture/emby-version.md](architecture/emby-version.md) |
| 搞清设置页为什么长这样、组标题为什么是空的 | [architecture/overview.md](architecture/overview.md) |
| 要不要合并上游 OneManJS 的分支 | [decisions/0001-不合并上游分支.md](decisions/0001-不合并上游分支.md) |

## 文档约定

**命名**

- `history/YYYY-MM/YYYY-MM-DD-<主题>.md` —— 日期在前，**版本号不进文件名**（版本号写在正文里）。
  历史上 `优化方案-v3` / `v4-实测版` / `汇总-v2` / `v11` 并存，谁是基线只能靠记忆，这个坑不再踩。
- `decisions/NNNN-<结论式标题>.md` —— 标题直接写结论，不写"关于……的决策"。
- 其余目录用英文短横线命名。

**写作**

- 结论放最前面，证据（日志原文、SQL 查询结果、页面 JSON 片段）贴在后面。
- 涉及 Emby 内部行为的结论，必须注明**是反编译看到的还是实机验证的**，两者可信度不同。
- 已失效的文档不要删，移到 `history/archive/` 并在标题下加一行 `> 已废弃，见 <新文档>`。

**状态标记**（写在正文开头）

| 标记 | 含义 |
|---|---|
| ✅ | 已实现且实机验证过 |
| 🟡 | 已实现，未完整验证 |
| ⬜ | 仅方案，未动工 |
| ❌ | 评估后决定不做（原因写清） |
