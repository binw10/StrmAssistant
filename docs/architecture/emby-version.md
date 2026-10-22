# Emby 版本适配

Emby 的内部实现（尤其是 `Emby.Server.Implementations` / `Emby.Providers` 里的私有方法）在小版本之间
也会变，而 Harmony 补丁是按方法签名打的。所以插件必须做版本判定，并给出"这个版本上哪些功能不可用"的降级。

## 三个组件

| 组件 | 位置 | 作用 |
|---|---|---|
| `EmbyApiVersion` | `Core/EmbyApiVersion.cs` | 把版本号归一成可比较的整数枚举（`V4_8_0` … `V4_10`），避免到处 `new Version("x.y.z")` 比较 |
| `EmbyVersionAdapter` | `Core/EmbyVersionAdapter.cs` | 统一入口：`TryLoadAssembly` / `TryGetType` / `FindCompatibleMethod` / `LogCompatibilityInfo` |
| `Plugin.AdvertisedEmbyVersion` | `Plugin.cs` | 对外声明支持的版本，当前 **`4.10.1.0`** |

**版本号编码**：`major × 1_000_000 + minor × 100_000 + build × 1_000 + revision`。
例：`4.9.1.90` → `4_901_090`；`4.10.0.0` → `5_000_000`（注意 4.10 跨过了 5,000,000）。

## 编译期 SDK

```xml
<PackageReference Include="mediabrowser.server.core" Version="4.9.1.90" />
```

- **不要跟着上游降回 4.8.10.0**。上游 OneManJS 的分支是这么干的，合并等于回退我们的 Emby 支持基线。
- 编译 SDK 版本与运行时 Emby 版本**不要求一致**，只要用到的 API 在运行版本里存在。
  所以适配工作大部分落在运行时反射 + 补丁签名匹配上，而不是改 SDK 引用。

## 适配矩阵

| Emby 版本 | 状态 | 说明 |
|---|---|---|
| 4.8.x | ❌ 不支持 | 上游分支保留了对 4.8.10 的枚举，本仓已不维护 |
| 4.9.1.90+ | ✅ | 历史基线，README 里声明支持 |
| 4.10.1.0 | ✅ 主要目标 | 当前开发与实机验证环境 |

## 版本差异导致的已知行为变化

改代码时遇到这几个点要留意：

1. **章节表**：4.10 是 `Chapters3`（4.9 及以前是 `Chapters2`）。直接用 SQL 查库时别写错。
2. **`GetChapters` 重载**：4.10 的 `ItemRepository.GetChapters(item, ct)` 接受 **item 实例**，
   不再用 `InternalId`。本仓已全部切到 item 重载。
3. **图片抽取器**：4.10 有 `SingleImageExtractor` / `ImageSeriesExtractor` /
   `QuickImageSeriesExtractor` / `MultiImageExtractor`，**没有** `QuickSingleImageExtractor`
   （某些文档里有这个名字，是错的）。
4. **`.strm` 是 shortcut**：`BaseMediaInfoProber.Probe` 里有
   `if (!item.IsShortcut || refreshOptions.EnableRemoteContentProbe)`，
   非插件流程一律跳过探测。这是"刷新没反应"的最常见原因。
5. **`extract-image` 进程 exit code 1 是既有行为**：`-vf thumbnail=N` 会往单一输出文件名写多帧，
   `image2` 报错。Emby 只看输出文件是否存在且非空，别把它当失败。

## 新增一个版本适配点时怎么做

1. 反编译目标版本，确认方法签名（`ilspycmd` 在 `C:\Users\liwl\.dotnet\tools\ilspycmd.exe`）。
2. 用 `EmbyVersionAdapter.Instance.FindCompatibleMethod(...)` 而不是裸 `GetMethod`，
   它会记录兼容性日志（`LogCompatibilityInfo`）。
3. 找不到方法时**不要抛异常**：`PatchTracker.FallbackPatchApproach = PatchApproach.None`
   + `IsSupported = false`，让该功能静默不可用，并在设置页把对应开关置灰（`[EnabledCondition]`）。
4. 在本文的矩阵里补一行。
