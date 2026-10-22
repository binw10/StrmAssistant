# 单元测试

`StrmAssistant.Tests/`（xunit，**net8.0**），当前 **59 例全绿**。

## 跑法（有顺序）

```bash
# 1. 先构建插件本体 —— 测试引用的是构建产物，不是源码
dotnet build StrmAssistant/StrmAssistant.csproj -c Release

# 2. 再跑测试
dotnet test StrmAssistant.Tests/StrmAssistant.Tests.csproj -c Release
```

## 为什么必须分两步

测试项目引用的是**已构建的合并程序集** `StrmAssistant/bin/Release/net6.0/StrmAssistant.dll`。

> 🔴 **不要改成 `ProjectReference`**。改了两件事会发生：触发 ILRepack（把测试项目也卷进合并流程），
> 以及把半成品部署到正在跑的 Emby。

代价是改完代码必须重新 `dotnet build` 才能被测试看到 —— 忘了就会觉得"我明明改了但测试还是旧行为"。

## 其它约定

- 测试项目**没有加入 `.sln`**。构建顺序有依赖，加进去会让 IDE 的"生成解决方案"乱序执行。
- `Properties/AssemblyInfo.cs` 里有 `InternalsVisibleTo("StrmAssistant.Tests")`。
- 需要访问 private 成员时，**把成员改成 `internal`**，不要用反射
  （`ParsePlayListMark` / `PlaylistEntry` / `ClipEntry` 都是这么处理的）。

## 现有测试

| 文件 | 覆盖 |
|---|---|
| `UdfTitleResolverMplsTests` | MPLS `PlayListMark` 解析、长度自洽、多 PlayItem 时间戳换算 |
| `LanguageUtilityTests` | 中文判定、繁简转换等 |
| `HttpRangeStreamTests` | 块边界跨读、非对齐 `Seek`、`SeekFromEnd`、流末尾返回 0、不支持 Range 的服务器返回 false、真实 ISO 经本地 Range 服务器解析出章节 |

`HttpRangeStreamTests` 内自带一个极简 TCP HTTP 服务器（支持 HEAD / Range / 文件模式），
用来端到端验证远程 ISO 链路。

> ⚠️ 那个服务器的 `Handle` 必须 `try/catch` 兜住 —— 客户端断开时抛的异常会把整个测试进程带崩。

## 什么该写单元测试，什么不该

**适合**：纯函数、协议解析（MPLS / UDF / Range 语义）、字符串与语言处理。

**不适合**：依赖 `Plugin.Instance` 的逻辑。目前没有 mock 基础设施，
`LibraryApi` 那类静态类很难测。真要测的话先引一层接口，不要为了测试硬造反射。

**实机验证优先**：涉及 Emby 内部行为的改动（补丁是否打上、章节是否落库、页面是否 500），
单元测试给不了结论，走 [verify-on-emby.md](verify-on-emby.md)。
