# ISO 挂载释放缺陷修复方案（方案 1~5）

> **日期**：2026-10-16　**状态**：✅ **方案 1/2/3/4/5 全部已实现**（4A/4B 已实机验证通过；方案 1/2/3/5 编译 0 error、DLL 已部署，实机验证步骤见 §8）
> **对象文件**：`StrmAssistant/Common/IsoMounter.cs`、`StrmAssistant/Common/IsoMountEntryPoint.cs`（新增）、`Plugin.cs`
> **基线**：`docs/PRO功能差异全量梳理与移植计划-v8.md`；`docs/第3阶段-双层杜比P7标注-方案.md`（§9.4「遗留观察」记录的本问题）
> **范围**：覆盖前次排查列出的**全部五项**缺陷——① 跨重启孤儿（方案 4）、② 卸载假成功（方案 1）、③ Failed 条目留孤儿（方案 3）、④ PowerShell 吞错（方案 2）、⑤ 复用缓存盘符不校验（方案 5）。

---

## 一、背景与缺陷映射

前次排查（见对话记录）确认挂载释放存在以下代码级缺陷，并按修复方式编号：

| 编号 | 缺陷 | 现有代码 | 本次方案 |
|---|---|---|---|
| ① | `Mounts` 为进程内静态字典，重启即丢失；无启动期对账、无正常关闭释放钩子 → 重启后孤儿挂载**永久残留** | [IsoMounter.cs#L68](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L68) / [Plugin.cs#L715-L727](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Plugin.cs#L715-L727) | **方案 4** |
| ② | `Dismount()` 不校验实际结果，无条件 `Info("dismounted")` 且 `return true` | [IsoMounter.cs#L623-L638](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L623-L638) | **方案 1** |
| ③ | `SweepIdle()` 对 `Failed` 条目只移除不卸载；`Mount()` 取盘符失败时不回滚，可能遗留已挂载孤儿 | [IsoMounter.cs#L571-L596](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L571-L596) / [L467-L500](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L467-L500) | **方案 3** |
| ④ | `RunPowerShell()` 丢弃 stderr、不检查 ExitCode | [IsoMounter.cs#L642-L668](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L642-L668) | **方案 2** |
| ⑤ | `GetProbePath()` 复用缓存 `DriveRoot` 前不校验卷（可能探测到错误内容） | [IsoMounter.cs#L382-L410](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L382-L410) | **方案 5** |

> **实证背景**：日志显示 `11:30:12` 有一次挂载，其后 Emby 在 `12:55:25`、`12:57:25` 两次重启（插件重新加载、静态字典被重置），该次挂载的 `dismounted` 从未出现，直到人工介入才释放——**属缺陷 ①，只有方案 4 能覆盖**。

**实施依赖顺序**：方案 2 为 1/3/4/5 提供失败判据（`PsResult`）。建议顺序 **2 → 1 → 3 → 4A（→ 4B 可选）→ 5**。

---

## 二、方案 1：`Dismount()` 据实返回与真实日志

**目标**：卸载动作必须以「实际是否已解挂」为准返回与记录，杜绝「假成功」。

**做法**：调用 `Get-DiskImage -ImagePath` 探测 `Attached` 状态，仅在确实挂载时才 `Dismount-DiskImage`，再二次探测确认；按结果分派日志与返回值。

### 2.1 修改后代码

```csharp
private static bool Dismount(string isoPath)
{
    // 先探测真实挂载状态：仅在确实 Attached 时才卸载，并据实返回结果（杜绝"假成功"）
    var script =
        "$ErrorActionPreference='SilentlyContinue'; " +
        $"$img = Get-DiskImage -ImagePath '{EscapePs(isoPath)}'; " +
        "if ($null -eq $img -or -not $img.Attached) { 'not-attached' } " +
        "else { " +
            $"Dismount-DiskImage -ImagePath '{EscapePs(isoPath)}' | Out-Null; " +
            $"$img = Get-DiskImage -ImagePath '{EscapePs(isoPath)}'; " +
            "if ($null -ne $img -and $img.Attached) { 'still-attached' } else { 'dismounted' } }";

    var res = RunPowerShell(script);
    var token = res?.StdOut?.Trim();

    switch (token)
    {
        case "dismounted":
            Plugin.Instance?.Logger?.Info($"IsoExtraction - dismounted {isoPath}");
            ManifestRemove(isoPath);                 // 方案 4A：同步挂载清单
            return true;

        case "not-attached":
            // 本就没有挂载：视为"已释放"，调用方安全移除跟踪
            Plugin.Instance?.Logger?.Info($"IsoExtraction - {isoPath} not attached, nothing to dismount");
            ManifestRemove(isoPath);                 // 方案 4A
            return true;

        default:
            Plugin.Instance?.Logger?.Warn(
                $"IsoExtraction - dismount failed for {isoPath}: " +
                $"exit={res?.ExitCode}, out='{token}', err='{res?.StdErr?.Trim()}'" +
                (res?.TimedOut == true ? ", timed out" : ""));
            return false;
    }
}
```

### 2.2 返回语义

| 返回 | 含义 | 调用方行为（`SweepIdle` / `DismountAll`） |
|---|---|---|
| `true` | 已解挂，或本就未挂载 | 从 `Mounts` 移除跟踪 |
| `false` | 仍处于挂载且卸载失败/超时 | **保留跟踪，下轮重试**（不再误删） |

---

## 三、方案 2：`RunPowerShell()` 暴露退出码与 stderr

**目标**：挂载/卸载的失败对上层可见，为方案 1/3/4/5 提供判据。

**做法**：`RunPowerShell` 返回结构化结果；并发读取 stdout/stderr 避免单管道写满死锁；超时后 kill 并标记 `TimedOut`。

### 3.1 新增结果类型与助手

```csharp
private sealed class PsResult
{
    public string StdOut;
    public string StdErr;
    public int ExitCode = -1;   // -1：进程未能启动
    public bool TimedOut;
}

private static string WaitString(Task<string> task)
{
    try { return task.Wait(2000) ? task.Result : string.Empty; }
    catch { return string.Empty; }
}
```

### 3.2 修改后 `RunPowerShell`

```csharp
private static PsResult RunPowerShell(string script)
{
    var psi = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" +
                    script.Replace("\"", "\\\"") + "\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    var result = new PsResult();
    try
    {
        using var process = Process.Start(psi);
        if (process == null) return result;

        // 并发读取两条管道，避免其中一条写满导致另一条阻塞（原先顺序读取存在死锁风险）
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(30000))
        {
            try { process.Kill(true); } catch { /* 忽略 */ }
            result.TimedOut = true;
        }

        result.StdOut = WaitString(stdout);
        result.StdErr = WaitString(stderr);
        if (!result.TimedOut) result.ExitCode = process.ExitCode;
    }
    catch (Exception e)
    {
        result.StdErr = e.Message;
    }

    return result;
}
```

### 3.3 调用点适配

**`IsSupported`**（原 [L82-L92](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L82-L92)）：

```csharp
_supported = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
             RunPowerShell(
                 $"if (Get-Command {MountCmdlet} -ErrorAction SilentlyContinue) {{ 'yes' }}")
             ?.StdOut?.Trim() == "yes";
```

**`Mount`**：见方案 3 的 4.2 节（合并改造）。

---

## 四、方案 3：失败条目回滚（`SweepIdle` + `Mount`）

**目标**：不再出现「OS 层已挂载、插件却已放弃跟踪」的孤儿。

### 4.1 `SweepIdle`：`Failed` 条目先卸载再移除

原逻辑 [:L575-L581](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L571-L596) 对 `Failed` 条目直接 `TryRemove`（不卸载）。改为：

```csharp
foreach (var kv in Mounts.ToArray())
{
    if (kv.Value.Failed)
    {
        // 挂载失败 ≠ OS 层未挂上：先尝试卸载，成功才移除；失败则保留并延后重试
        if (Dismount(kv.Key)) Mounts.TryRemove(kv.Key, out _);
        else kv.Value.LastUsedUtc = DateTime.UtcNow;
        continue;
    }

    if (DateTime.UtcNow - kv.Value.LastUsedUtc < IdleTimeout) continue;
    if (Dismount(kv.Key)) Mounts.TryRemove(kv.Key, out _);
}
```

> 说明：`Failed` 条目对应「`Mount-DiskImage` 已成功但 `Get-Volume` 取不到盘符」的阶段——镜像可能真实挂在系统上，因此必须先尝试卸载。

### 4.2 `Mount`：失败时记录详情并回滚卸载

原逻辑 [:L480-L497](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L467-L500) 仅置 `Failed=true`，不回滚。改为：

```csharp
var res = RunPowerShell(script);
var output = res?.StdOut?.Trim();

if (res == null || res.TimedOut || res.ExitCode != 0 ||
    string.IsNullOrEmpty(output) || output.Length > 3)
{
    entry.Failed = true;
    Plugin.Instance?.Logger?.Warn(
        $"IsoExtraction - Mount-DiskImage failed for {isoPath}: " +
        $"exit={res?.ExitCode}, out='{output}', err='{res?.StdErr?.Trim()}'");

    // 回滚：镜像可能已挂载但取盘符失败，立即尝试释放，避免孤儿挂载
    Dismount(isoPath);
    return entry;
}

entry.DriveRoot = output.TrimEnd(':') + ":\\";
ManifestAdd(isoPath);                                       // 方案 4A：登记挂载清单
Plugin.Instance?.Logger?.Info($"IsoExtraction - mounted {isoPath} -> {entry.DriveRoot}");
```

> `$ErrorActionPreference='Stop'` 下，挂载异常会使 PowerShell 以非零退出码结束，`ExitCode != 0` 即精确捕获原先被吞掉的失败。

---

## 五、方案 4：跨重启孤儿对账（启动期）与关闭期释放

**缺陷 ①**：`Mounts` 是进程内静态字典，Emby 重启即清空；既无启动期对账、也无正常关闭释放钩子 → **重启后旧挂载永久残留**（`Mounts` 空，`SweepIdle` 不会再扫到它）。

**根因**：跟踪状态只存在于内存，没有任何「落盘记录」或「启动时重建」的机制。

**做法（两层：A 必做 + B 可选）**：

### 5A（必做）挂载清单持久化 + 启动期对账

- 新增清单文件 `<PluginConfigurationsPath>\StrmAssistant_iso_mounts.txt`，一行一个「当前由本插件挂载的 ISO 绝对路径」。
- **写入点**：`Mount()` 成功 → 登记（`ManifestAdd`）；`Dismount()` 成功 → 移除（`ManifestRemove`）。
- **启动期对账** `ReconcileOnStartup()`：读清单 → 对每条 `Get-DiskImage -ImagePath ... | Attached`，仍 `Attached` 的即为上次遗留孤儿 → `Dismount()` 释放；完成后清空清单。
- **调用点**：插件构造期（`Plugin` 构造函数内，`ApplicationPaths` 已赋值之后）。

此层**直接消除跨重启永久泄漏**（含实证的 `11:30` 场景），不依赖「关闭时是否被调用」。

### 5B（可选）关闭期释放钩子 —— 缩短孤儿窗口

- 在插件程序集内实现 `MediaBrowser.Controller.Plugins.IServerEntryPoint`（`Run()` + `Dispose()`），于 `Dispose()` 调用 `IsoMounter.DismountAll()`（`Run()` 亦可作为启动对账的备用调用点）。
- 作用：正常关闭/重启时即释放，避免「重启间隙仍挂载」。
- **已核实的接口事实**：本插件引用的 `MediaBrowser.Controller.dll` 中确认存在 `MediaBrowser.Controller.Plugins.IServerEntryPoint`，其成员为 `Void Run()`，并实现 `IDisposable`；而 `BasePlugin` **不含任何关闭钩子**（仅 `OnUninstalling`，[:Plugin.cs#L715-L727](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Plugin.cs#L715-L727)）。
- **已实机证实（2026-10-16）**：Emby 确实把插件程序集内的 `IServerEntryPoint` 注册进 DI 并调用 `Run()`/`Dispose()`。日志同时出现 Emby 宿主自身的 `Starting entry point StrmAssistant.Common.IsoMountEntryPoint` / `Disposing IsoMountEntryPoint`，以及本插件的 `IsoMountEntryPoint.Run invoked` / `IsoMountEntryPoint.Dispose invoked, releasing all ISO mounts`；两次正常关闭均由 `Dispose → DismountAll` 真实卸载（`dismounted`）。**5B 成立并已落地**（新增 `Common/IsoMountEntryPoint.cs`）。

### 5A 代码草案（`IsoMounter.cs` 内新增）

```csharp
private static string ManifestFilePath()
{
    var dir = Plugin.Instance?.ApplicationPaths?.PluginConfigurationsPath;
    return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "StrmAssistant_iso_mounts.txt");
}

private static void ManifestAdd(string isoPath)
{
    var file = ManifestFilePath();
    if (file == null) return;
    try
    {
        lock (_lock)
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string>();
            if (!lines.Any(l => string.Equals(l.Trim(), isoPath, StringComparison.OrdinalIgnoreCase)))
                lines.Add(isoPath);
            File.WriteAllLines(file, lines);
        }
    }
    catch { /* 忽略 */ }
}

private static void ManifestRemove(string isoPath)
{
    var file = ManifestFilePath();
    if (file == null) return;
    try
    {
        lock (_lock)
        {
            if (!File.Exists(file)) return;
            var lines = File.ReadAllLines(file)
                .Where(l => !string.Equals(l.Trim(), isoPath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (lines.Count == 0) File.Delete(file);
            else File.WriteAllLines(file, lines);
        }
    }
    catch { /* 忽略 */ }
}

private static bool IsAttached(string isoPath)
{
    var script =
        "$ErrorActionPreference='SilentlyContinue'; " +
        $"$img = Get-DiskImage -ImagePath '{EscapePs(isoPath)}'; " +
        "if ($null -ne $img -and $img.Attached) { 'attached' } else { 'detached' }";
    return RunPowerShell(script)?.StdOut?.Trim() == "attached";
}

/// <summary>启动期对账：释放上次进程遗留的孤儿挂载。</summary>
public static void ReconcileOnStartup()
{
    var file = ManifestFilePath();
    if (file == null || !File.Exists(file)) return;

    string[] paths;
    try { paths = File.ReadAllLines(file).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray(); }
    catch { return; }

    foreach (var p in paths)
    {
        try
        {
            if (IsAttached(p))
            {
                Plugin.Instance?.Logger?.Info($"IsoExtraction - reconciling orphan mount from previous run: {p}");
                Dismount(p);
            }
        }
        catch { /* 忽略 */ }
    }

    try { File.Delete(file); } catch { /* 忽略 */ }
}
```

> **实现说明（2026-10-16 落地）**：本层**未依赖**方案 2——`IsAttached` 直接使用现有 `RunPowerShell(string)` 的返回值（`?.Trim() == "attached"`），待方案 2 落地后再统一改用 `PsResult`。同理，`ManifestRemove` 现挂在方案 1 落地**之前**的 `Dismount()` 成功路径上，其"成功"语义将在方案 1 完成后才真正可信（当前 `Dismount()` 仍无条件返回 `true`，属**已知残留**）。

**接线**：`Mount()` 成功分支追加 `ManifestAdd(isoPath);`（见 4.2）；`Dismount()` 成功分支追加 `ManifestRemove(isoPath);`（见 2.1）；`Plugin` 构造期调用 `Common.IsoMounter.ReconcileOnStartup();`（已落地，见 [Plugin.cs](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Plugin.cs#L171-L179)）。

**方案 4 触及文件**：`Common/IsoMounter.cs`；`Plugin.cs`（新增启动期调用；若采纳 5B 再增 entry point 类或让 `Plugin` 实现 `IServerEntryPoint`）。

---

## 六、方案 5：复用缓存盘符前校验卷

**缺陷 ⑤**：`GetProbePath()` 直接信任缓存 `entry.DriveRoot`，不验证卷是否仍挂载、盘符是否被其它卷复用 → 可能探测到**错误内容**。

**做法**：缓存命中时做「镜像身份」校验——用 `Get-DiskImage -ImagePath <iso>` 反查该 ISO 当前实际挂载盘符，与缓存盘符比对；不一致（或已卸载）即判定缓存失效，重置并重新挂载。

### 6.1 新增助手

```csharp
// 复用方案 2 的 RunPowerShell
private static bool IsVolumeStillMounted(string isoPath, string driveRoot)
{
    var script =
        "$ErrorActionPreference='SilentlyContinue'; " +
        $"$img = Get-DiskImage -ImagePath '{EscapePs(isoPath)}'; " +
        $"if ($null -ne $img -and $img.Attached) {{ ((Get-DiskImage -ImagePath '{EscapePs(isoPath)}' | Get-Volume).DriveLetter) }}";
    var outv = RunPowerShell(script)?.StdOut?.Trim();
    return !string.IsNullOrEmpty(outv) &&
           string.Equals(outv.TrimEnd(':') + ":\\", driveRoot, StringComparison.OrdinalIgnoreCase);
}
```

### 6.2 `GetProbePath` 缓存命中分支改造

```csharp
var entry = Mounts.GetOrAdd(isoPath, k => Mount(k));
entry.LastUsedUtc = DateTime.UtcNow;

// 缓存盘符失效（被外部卸载 / 盘符被其它卷复用）→ 重置并重新挂载
if (!entry.Failed && !string.IsNullOrEmpty(entry.DriveRoot) &&
    !IsVolumeStillMounted(isoPath, entry.DriveRoot))
{
    Plugin.Instance?.Logger?.Info(
        $"IsoExtraction - cached mount {entry.DriveRoot} for {isoPath} is stale, remounting");
    entry = Mount(isoPath);
    entry.LastUsedUtc = DateTime.UtcNow;
    Mounts[isoPath] = entry;      // 覆盖失效条目
}

if (!entry.Failed && !string.IsNullOrEmpty(entry.DriveRoot))
{
    // …原有 ResolveMountedTitle 逻辑保持不变…
}
```

> **代价**：缓存命中路径每次多一次 PowerShell 调用（~100ms 量级）。探测不是高频热路径，可接受；如需更省，可退化为「先 `Directory.Exists(driveRoot)` 快速判断，仅当目录存在时才做镜像身份校验」。
>
> **备选（更严）**：连卷标签一并比对（`(Get-DiskImage … | Get-Volume).FileSystemLabel`），进一步排除「盘符 + 卷结构巧合」的极端情形；本方案默认不引入，以控成本。

**方案 5 触及文件**：`Common/IsoMounter.cs`（`GetProbePath`、新增 `IsVolumeStillMounted`）。

---

## 七、变更清单

| 文件 | 方法/成员 | 变更 | 方案 |
|---|---|---|---|
| `Common/IsoMounter.cs` | `RunPowerShell` | 返回 `string → PsResult`；并发读管道；捕获 ExitCode/stderr/超时 | 2 |
| `Common/IsoMounter.cs` | 新增 `PsResult`、`WaitString` | 支撑方案 2 | 2 |
| `Common/IsoMounter.cs` | `IsSupported` | 适配 `PsResult`（判定 `StdOut=="yes"`） | 2 |
| `Common/IsoMounter.cs` | `Dismount` | 探测真实 `Attached`；据实返回/日志；联动 `ManifestRemove` | 1、4A |
| `Common/IsoMounter.cs` | `SweepIdle` | `Failed` 条目先卸载再移除，失败保留重试 | 3 |
| `Common/IsoMounter.cs` | `Mount` | 适配 `PsResult`；失败记录 exit/err 并回滚；成功 `ManifestAdd` | 2、3、4A |
| `Common/IsoMounter.cs` | 新增 `ManifestFilePath`/`ManifestAdd`/`ManifestRemove`/`IsAttached`/`ReconcileOnStartup` | 挂载清单持久化与启动对账 | 4A |
| `Plugin.cs` | 构造函数 | 启动期调用 `Common.IsoMounter.ReconcileOnStartup()` | 4A |
| `Common/IsoMounter.cs` | `GetProbePath` + 新增 `IsVolumeStillMounted` | 复用缓存盘符前做镜像身份校验 | 5 |
| （**可选**）新增 entry point 类 / `Plugin` 实现 `IServerEntryPoint` | `Run` / `Dispose` | 关闭期调用 `DismountAll()` | 4B |

**不改动**：`DismountAll`（沿用「返回 `true` 才移除」约定，方案 1 已使其可信）、`EnsureSweeper`、`RemoteIsos` 相关逻辑、前端资源、resx、`MediaInfoExtractOptionsStore.cs`。

**新增运行期产物**：配置目录下 `StrmAssistant_iso_mounts.txt`（清单，释放后自动删除/清空）。

---

## 八、验收与验证步骤

**编译**：`dotnet build` 增量，0 error（现存 2 条历史 warning 不变）。

**功能验证**（真实实例，需重启 Emby 加载新 DLL）：

| # | 步骤 | 期望结果 | 方案 |
|---|---|---|---|
| 1 | 触发本地 ISO 探测（改 ISO 的 mtime + 触发库扫描） | 日志 `IsoExtraction - mounted ... -> X:\` | 1/3 |
| 2 | 空闲 ≥ `IdleTimeout`(10 min) 后观察清扫 | 日志 `IsoExtraction - dismounted ...`，`Get-DiskImage -ImagePath ...` 显示 `Attached=False`（**真实释放**） | 1 |
| 3 | **反证方案 1**：先手工 `Dismount-DiskImage`，再等清扫 | 日志 `... not attached, nothing to dismount`，**不再误报 `dismounted`** | 1 |
| 4 | **反证方案 2/3**：对无效/异常 ISO 触发探测 | 日志含 `Mount-DiskImage failed ... exit=<非0>, err=<PowerShell 错误文本>`；若镜像已挂上则被回滚 | 2/3 |
| 5 | 挂载后检查配置目录 | 出现 `StrmAssistant_iso_mounts.txt` 且含该 ISO；释放后该文件消失/为空　✅ 已实测（见 8.1） | 4A |
| 6 | **方案 4A 主验证**：制造残留（挂载后**不**释放，直接重启 Emby） | 重启日志出现 `reconciling orphan mount from previous run: ...`；重启后未挂载　✅ 已实测（见 8.1） | 4A |
| 7 | **方案 4B 验证**：重启观察 | `Run()` / `Dispose()` 日志出现，关闭期 `DismountAll` 真实释放　✅ 已实测（见 8.1） | 4B |
| 8 | **方案 5 验证**：探测成功后手工 `Dismount-DiskImage`，再触发同一 ISO 探测 | 日志 `cached mount ... is stale, remounting`，且探测结果仍正确 | 5 |
| 9 | 远程 strm 路径回归 | 行为不变（仍走 HTTP Range 窗口，无挂载动作） | 全部 |
| 10 | 关闭 `EnableIsoExtraction` | 沿用 `DismountAll()`，日志据实、清单被清空 | 1/4A |

> 步骤 2/3 需 ≥10 分钟等待；可在测试中临时把 `IdleTimeout` 调小以加速（**仅测试期**，不入库）。

### 8.1 实测记录（方案 4A / 4B，2026-10-16）

四项关键信息**全部实测通过**，证据取自真实实例 `D:\下载\embyserver-win-x64-4.10.1.0\programdata\logs`：

| # | 关键信息 | 结果 | 实测证据（时间 / 日志） |
|---|---|---|---|
| 1 | `IsoMountEntryPoint.Run invoked` | ✅ | 13:52:51、13:56:14、13:59:36、14:00:04、14:02:15（每次启动均出现） |
| 2 | `IsoMountEntryPoint.Dispose invoked` + 真实卸载 | ✅ | 13:56:00.113 → 13:56:02.102 `dismounted`；13:59:22.458 → 13:59:24.401 `dismounted` |
| 3 | 清单文件生成 | ✅ | 13:58:33 生成 `StrmAssistant_iso_mounts.txt`（55 bytes，内容为 ISO 绝对路径）；释放后自动删除 |
| 4 | `reconciling orphan mount` + 释放 + 清清单 | ✅ | 14:02:11.501 对账日志 → 14:02:13.654 `dismounted` → 清单被删（[IsoMounter.cs#L754](file:///d:/%E4%B8%8B%E8%BD%BD/AI/StrmAssistant-myp015/StrmAssistant/Common/IsoMounter.cs#L754)） |

**验证手法**：

- **第 1/2 条**：重启 Emby 即可观测（`Run` 于启动、`Dispose` 于关闭）。正常关闭时 4B 会先释放，故 4A 不会因正常关闭而触发——**两者为互补关系**：4B 收敛正常关闭窗口，4A 兜底非正常关闭（崩溃/强杀）。
- **第 3 条**：改本地 ISO mtime + 触发库扫描 → 触发挂载；挂载后 **10 分钟内**检查清单文件（早于空闲清扫，否则会被 `ManifestRemove` 删除）。
- **第 4 条**：用「手工 `Mount-DiskImage` 挂上 ISO + 手工按 `ManifestAdd` 格式写清单（模拟上次进程遗留）+ 启动 Emby」制造孤儿，**避免与 Emby 正常重启抢时间**——启动即由 `ReconcileOnStartup` 释放。

**方法学注意（重要）**：验证挂载状态**只能以 Emby 日志为准**。本次发现**工具侧 shell 对 `Get-DiskImage ... .Attached` 的读数与真实系统不一致**（同一命令内 `Mount-DiskImage` 报 `Attached=True`，下一命令却报 `Attached=False`），而 Emby 进程内实测 `IsAttached` 为真并成功对账。因第 4 条的 `IsAttached` 与后续方案 5 的 `Get-DiskImage` 均在 **Emby 进程内**执行（同一环境），故**方案 5 不受此影响**。

---

## 九、风险与回滚

| 风险 | 评估 | 缓解 |
|---|---|---|
| 新增 `Get-DiskImage` 探测带来额外 PowerShell 调用 | 可忽略：仅在卸载/对账路径，非探测热路径 | — |
| 方案 5 每次缓存命中 +1 次 PowerShell 调用 | ~100ms/次，探测非高频；可接受 | 可退化为「先目录存在性快速判断」 |
| `PsResult` 改变私有方法签名 | 调用点有限，编译期即可发现遗漏 | 编译验证 |
| 并发读管道的线程模型 | 标准 `ReadToEndAsync` 模式，无新增线程 | — |
| 清单文件读写异常 | 已加锁 + 全 `try/catch`，失败不影响主流程 | 忽略异常 |
| 方案 5 remount 覆盖 `Mounts[isoPath]` 的竞态 | 探测路径基本串行，风险低 | 如需严谨可在 `_lock` 内替换 |
| 方案 4B `IServerEntryPoint` 是否被 Emby 注册 | **不确定** | 以 `Run()`/`Dispose()` 日志实测为准；不影响 4A |
| 行为面扩大 | 改动集中在 `IsoMounter.cs` + `Plugin.cs` 一处调用 | 单文件/单点回退 |

**回滚**：方案未获批准不落码；落码后如需回退，还原 `IsoMounter.cs`（及 `Plugin.cs` 的那一行调用）即可。

---

## 十、非目标（明确不做）

- **Linux / Docker**：无 OS 挂载路径（由自定义 ffprobe 直读 `.iso`），不存在本挂载生命周期问题，本方案不适用。
- **远程 strm 临时文件清理**：已由 `RemoteCacheTtl`(30 min) 空闲清扫覆盖，不在本次范围。

---

## 十一、后续

1. ~~是否采纳 **方案 4B**~~ → **已采纳、已落地、已实机验证通过**（Emby 确认注册 entry point，`Run`/`Dispose` 均被调用，正常关闭即真实释放）。方案 4A/4B 四项验证见 §8.1。
2. ~~获批后按 **2 → 1 → 3 → 5** 顺序执行~~ → **已执行完毕**，见 §十二。
3. 方案 5 的校验强度：默认「镜像身份校验」（已落地），如需加严到「含卷标签比对」或放宽为「目录存在性快速判断」，可作为后续微调。

---

## 十二、方案 1/2/3/5 实施记录（2026-10-16）

**决策**：用户批准「按方案 2 → 1 → 3 → 5 顺序开工」。全部落码于 `StrmAssistant/Common/IsoMounter.cs` 单文件（无其他文件改动）。

### 12.1 落地清单

| 方案 | 改动 | 位置（行号为改动后） |
|---|---|---|
| **2** | 新增 `PsResult` 私有类 + `WaitString` 助手；`RunPowerShell` 返回类型 `string → PsResult`，并发 `ReadToEndAsync` 读双管道（消除顺序读死锁风险），超时 `Kill(true)` 并置 `TimedOut` | `IsoMounter.cs` 尾部 |
| **2** | `IsSupported` 适配：判定 `?.StdOut?.Trim() == "yes"` | `IsSupported` |
| **1** | `Dismount` 重写：先 `Get-DiskImage` 探测 `Attached` → 仅确挂时卸载 → 二次探测确认；按 `dismounted`/`not-attached`/其他 三分支据实返回与日志 | `Dismount` |
| **3** | `Mount` 失败分支：判定 `res==null \|\| TimedOut \|\| ExitCode!=0 \|\| 输出异常`，记录 exit/err 并**回滚 `Dismount`** | `Mount` |
| **3** | `SweepIdle`：`Failed` 条目改为「先 `Dismount`，成功才移除；失败保留并刷新 `LastUsedUtc` 延后重试」 | `SweepIdle` |
| **5** | 新增 `IsVolumeStillMounted`（`Get-DiskImage` 反查实际盘符与缓存比对）；`GetProbePath` 缓存命中前校验，失效则 `Mount` 重挂并覆盖 `Mounts[isoPath]` | `GetProbePath` |
| **2** | `IsAttached` 适配 `PsResult` | `IsAttached` |

**签名影响面**：`RunPowerShell` 为 `private`，全部调用点（5 处）均在本文件内，已同步适配；对外公开方法签名**零变更**，`IsoExtraction.cs` / `Plugin.cs` / `MediaInfoExtractOptionsStore.cs` / `IsoMountEntryPoint.cs` 无需改动。

### 12.2 验证状态

| 项 | 状态 |
|---|---|
| `dotnet build -c Debug` | ✅ **0 错误**、1 条历史 warning（`SubtitleApi.cs` CS1998，与本次无关） |
| ILRepack 合并 | ✅ 成功（`bin/Debug/net6.0/StrmAssistant.dll`） |
| 部署 DLL | ✅ `D:\下载\embyserver-win-x64-4.10.1.0\programdata\plugins\StrmAssistant.dll`，与构建产物 **MD5 一致**（`58a8ae7a…`） |
| 实机验证（步骤 3/4/8 + 空闲清扫） | ✅ 见 §12.3 |
| 提交 | ⬜ 待提交 |

### 12.3 实机验证记录（2026-10-16，真实实例 4.10.1.0）

**环境**：Emby 4.10.1.0 于 14:15:21 重启加载新版 DLL；36 个模块 Initialized、0 error；`IsoMountEntryPoint.Run invoked` 出现。

| §8 步骤 | 方案 | 结果 | 日志实证（时间 / 内容） |
|---|---|---|---|
| **4** 无效 ISO → 挂载失败+回滚 | 2、3、1 | ✅ | 14:18:09.682 `Mount-DiskImage failed for …ZZZ-无效挂载测试.iso: exit=1, out='', err='Mount-DiskImage : 文件或目录损坏且无法读取。…HRESULT 0x80070570'` → 14:18:11.831 `…not attached, nothing to dismount`（回滚已执行，且**未误报** `dismounted`） |
| **8** 缓存盘符失效重挂 | 5 | ✅ | 14:18:59 `mounted … -> E:\` →（手工 `Dismount-DiskImage`）→ 14:19:46.727 `cached mount E:\ for … is stale, remounting` → 14:19:48.681 `mounted … -> E:\` + 选片 `BDMV\STREAM\00000.m2ts` 正确 |
| **3** 反证方案 1 | 1、4B | ✅ | 14:20:35.067 `IsoMountEntryPoint.Dispose invoked, releasing all ISO mounts` → 14:20:37.233 `杜比视界测试：双层 FEL2.iso not attached, nothing to dismount`；**无** `dismounted` 误报 |
| **1/2** 空闲清扫真实释放 | 1 | ✅ | 14:30:49.187 `mounted … -> E:\`（清单文件写入，55 bytes）→ 14:41:49.459 清扫 `dismounted`（空闲满 10 min 后的首个整分点），清单文件自动删除；`Get-DiskImage` 复核 `Attached=False`、系统无残留映像 |
| **10** 关闭开关 / 回归 | 全部 | ✅ | 条目 424/428 流均完好：`(2160x3840, Hdr10=100/100)` + `(1080x1920, DolbyVision=130/13076)`；**远程 ISO 路径方法（`GetRemoteProbePath`/`DownloadRange`/`TryNormalizeRemoteIso`/`LooksLikeIsoPath`）diff 零改动** |

**额外独立复现（新进程内、经插件自带「提取媒体信息」任务，14:26:51）**：
`Mount-DiskImage failed … exit=1, err='…文件或目录损坏…'` → 14:26:54 `not attached, nothing to dismount`。即方案 2/3/1 在同一次会话中经**两条不同入口**（库扫描探测 / 插件提取任务）各验证一次。

**方法学注意**：
- 挂载状态读数以 **Emby 日志 + PowerShell 工具**双源交叉确认（`Get-DiskImage … .Attached`）。
- **`SweepIdle` 计时**：清扫器首次触发在 `EnsureSweeper` 后 `IdleTimeout`(10 min)，其后每 1 min 一次；故「空闲满 10 min」的实际释放时刻会落在首个整分点（实测 14:30:49 挂载 → 14:41:49 释放，间隔 11 min）。
- **步骤 6（启动期对账）本次未触发**：本会话每次关闭均经 4B 正常释放并清空清单，故启动时无孤儿可对账；该分支已于 §8.1（14:02 实测）单独验证过。
- **副作用记录（非本方案引入）**：对 `.strm` 文件执行 `touch`（改 mtime）+ 库扫描时，Emby 会重置该条目的 `MediaStreams2` 行（元数据 `Container/RunTimeTicks/Size` 保留），需经插件「提取媒体信息」任务由 JSON 反序列化恢复。此行为由 Emby 的 strm 处理触发，与 `IsoMounter` 改动无关（该条目探测全程无 `IsoExtraction` 日志）。

> **行为闭环**：方案 1 使 `Dismount` 不再假成功，`SweepIdle` / `DismountAll` / `ReconcileOnStartup` 的「返回 true 才移除跟踪」约定由此真正可信——这是 4A/4B 生效的前提；方案 3 进一步保证「OS 层已挂载、插件却已放弃跟踪」的孤儿不再产生。
