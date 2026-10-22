# 贡献指南

## 环境

- .NET SDK（项目目标框架 `net6.0`，测试项目 `net8.0`）
- 一个可重启的 Emby 4.9.1.90+ 实例（当前开发基线 4.10.1.0）

## 构建与测试

```bash
dotnet build StrmAssistant/StrmAssistant.csproj -c Release   # 自动部署到 Emby plugins 目录
dotnet test  StrmAssistant.Tests/StrmAssistant.Tests.csproj -c Release
```

顺序不能反：测试引用的是**构建产物**，不是源码。详见 [docs/dev/testing.md](docs/dev/testing.md)。

## 提交前自查

- [ ] `dotnet build` 无新增 warning（已知仅有 `SubtitleApi.cs` 的 `CS1998`）
- [ ] 构建输出 `[OK] resx keys consistent`
- [ ] `dotnet test` 全绿
- [ ] 涉及 Emby 内部行为的改动，在真机上验证过，并把日志原文贴进提交信息
- [ ] 新增选项同步了三语 resx（Designer 会自动补）

## 提交信息

Conventional Commits 前缀：`feat` / `fix` / `perf` / `refactor` / `docs` / `chore` / `ci`。

正文说清三件事：

1. **改了什么**（行为层面的变化，不是"改了某文件"）
2. **为什么**（尤其是"看起来可以那样做但不行"的取舍）
3. **怎么验证的**（日志、SQL 结果、页面状态码 —— 贴原文）

示例：

```
perf: 队列批处理内存回收

task 列表超过 100 条即剔除已完成的 task，否则闭包捕获的 BaseItem 会一直活到
Task.WhenAll 返回，大批量刮削期间 RSS 持续上涨。

验证：追更队列实跑 48 条，'Clear Item Queue Stopped' 后 175ms 出现
'MemoryCleaner [SetProcessWorkingSetSize]: ... RSS 1 MB (freed 71 MB)'
```

## 不要做的事

- **不要合并上游 `OneManJS/main`**。原因见 [docs/decisions/0001-不合并上游分支.md](docs/decisions/0001-不合并上游分支.md)。
- **不要给 `StrmAssistant.Tests` 加 `ProjectReference`**，会触发 ILRepack 并把半成品部署到 Emby。
- **不要用空字符串"省略"分组标题**，会渲染成没有标题的折叠块。见 [ADR 0004](docs/decisions/0004-resx-标题留空不等于隐藏.md)。
- **涉及视觉的改动（对齐/缩进/配色）先截图确认再落码**，只凭 JSON 结构推断视觉会翻车。

## 文档

改代码时顺手更新对应文档，别让 `docs/` 落后于实现：

| 改动 | 要更新的文档 |
|---|---|
| 新增/删除补丁 | [docs/architecture/patching.md](docs/architecture/patching.md) 的清单 |
| 改中文搜索链路 | [docs/architecture/chinese-search.md](docs/architecture/chinese-search.md) |
| 改 ISO 链路 | [docs/architecture/iso.md](docs/architecture/iso.md) |
| 适配新 Emby 版本 | [docs/architecture/emby-version.md](docs/architecture/emby-version.md) 的矩阵 |
| 加选项 / 改单位 | 不需要改文档，但请遵守 [docs/dev/options-conventions.md](docs/dev/options-conventions.md) |
| 做了架构级取舍 | 在 `docs/decisions/` 新增一篇 ADR |
| 完成一个阶段 | 在 `docs/history/YYYY-MM/` 留一份记录（命名 `YYYY-MM-DD-<主题>.md`） |
