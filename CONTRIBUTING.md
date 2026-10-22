# 贡献指南

## 环境

- .NET SDK（项目目标框架 `net6.0`，测试项目 `net8.0`）
- 一个可重启的 Emby 4.9.1.90+ 实例（当前开发基线 4.10.1.0）

## 构建与测试

```bash
dotnet build StrmAssistant/StrmAssistant.csproj -c Release   # 自动部署到 Emby plugins 目录
dotnet test  StrmAssistant.Tests/StrmAssistant.Tests.csproj -c Release
```

顺序不能反：测试引用的是**构建产物**，不是源码。

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

- **不要合并外部主分支**。对方把 SDK 从 `mediabrowser.server.core 4.9.1.90` 降回 `4.8.10.0`，
  还带了闭源二进制；本仓已分叉 190+ 提交，合并等于回退。
- **不要给 `StrmAssistant.Tests` 加 `ProjectReference`**，会触发 ILRepack 并把半成品部署到 Emby。
- **不要用空字符串"省略"分组标题**，会渲染成没有标题的折叠块（`[DisplayNameL]` 的 resx 值为空串时，
  页面上是一块没有标题的缩进区域）。要去掉标题必须把那层容器拆掉。
- **涉及视觉的改动（对齐/缩进/配色）先截图确认再落码**，只凭 JSON 结构推断视觉会翻车。

## 文档

内部设计文档（架构说明、ADR、开发者手册、历史实施记录）**保留在本地 `docs/`，不发布到仓库**，
已在 `.gitignore` 中忽略。修改代码时如果发现文档与实现不符，直接在本地更新即可，不要提交 `docs/`。
