# 构建与部署

## 一条命令

```bash
dotnet build StrmAssistant/StrmAssistant.csproj -c Release
```

Windows 下这条命令会**自动部署**到 `D:\下载\embyserver-win-x64-4.10.1.0\programdata\plugins`，
不需要手工拷 dll。换目标目录：

```bash
dotnet build StrmAssistant/StrmAssistant.csproj -c Release /p:StrmAssistantPluginOutputDir=<你的 plugins 目录>
```

## 构建过程做了什么

`PostBuildMerge` 目标（`AfterTargets="Build"`）依次执行：

1. **ILRepack 合并**：把 `0Harmony`、`ChineseConverter`、`TinyPinyin`、`DiscUtils.*`（Core/Streams/Udf/Iso9660）
   全部并进 `StrmAssistant.dll`，产出单文件插件。
2. **生成版本化产物**：`StrmAssistantPlus_v<版本>.dll`，用于发布。
3. **部署**：拷到 `StrmAssistantPluginOutputDir`。

所以插件目录里**只需要一个 dll**（外加 `libsimple` 原生库）。

## 资源键自动修复（`CheckResxKeys`）

挂在 `BeforeTargets="CoreCompile"`，执行 `tools/check_resx_keys.py --quiet --fix`：

- 「代码引用 + resx 有 + `Resources.Designer.cs` 缺」的属性会被**自动补齐**。
- 其余问题（resx 缺键、缺译文）打印 `[FAIL]`，但不阻断构建（`ContinueOnError`）。
- 正常输出：`[OK] resx keys consistent`。

> 🔴 这个目标**必须挂 `CoreCompile`，不能挂 `Build`**。`Build` 的 `DependsOnTargets`（含 `CoreCompile`）
> 先执行完，挂 `Build` 上的钩子永远晚于编译 —— 会出现"修了但编译已经失败"。

> 构建期输出用 ASCII。GBK 控制台下中文会变成 `[OK] 璧勬簮閿竴鑷?` 这种乱码。

## 改完代码怎么生效

```
dotnet build -c Release   →  dll 已落到 plugins 目录
    ↓
重启 Emby                 → 插件才重新加载
```

**插件选项也是独立 JSON**（`programdata/plugins/configurations/StrmAssistant*.json`），
没有标准配置接口，改完同样要重启。

## 本地启动 Emby（开发用）

```bash
cd "D:/下载/embyserver-win-x64-4.10.1.0/system"
./EmbyServer.exe "D:/下载/embyserver-win-x64-4.10.1.0/system/EmbyServer.dll" -noautorunwebapp
```

> 🔴 **必须带 `-noautorunwebapp`**。不带的话 apphost 会再派生真正的服务进程，
> 而后台任务只托管了父进程，约 10 分钟后 Emby 就"消失"了（实际还在跑，但你看不到它的输出）。

日志：`programdata/logs/embyserver.txt`，重启时旧文件轮转为 `embyserver-<epoch>.txt`。

## Linux 构建

`build-local.sh` 是 Linux 下的变通脚本，处理两个已知问题：

1. `Resource.Embedder 2.2.0` 在 Linux 上有 bug（反斜杠路径），构建时临时移除，
   改为把 zh / zh-hant 卫星资源 DLL 拷在旁边（Emby 会加载）。
2. `ILRepack.exe` 是 .NET Framework 可执行文件，需要通过 `dotnet ILRepack.exe` 调用。

## 编译产物里的原生库

`Tokenizer/` 下的 `libsimple` 按平台放置（`win/`、`linux/`、`linux-arm64/`）。
**换平台或换 sqlite 版本要重新编译**，否则 `sqlite3_load_extension` 会失败。
编译方法见 README「编译 libsimple.so」。

## 版本号

`StrmAssistant/StrmAssistant.csproj` 里四个字段要一起改：

```xml
<AssemblyVersion>3.0.0.58</AssemblyVersion>
<FileVersion>3.0.0.58</FileVersion>
<Version>3.0.0.58</Version>
<InformationalVersion>3.0.0.58</InformationalVersion>
```

四段式，对齐 Releases tag。`Plugin.AdvertisedEmbyVersion` 是**另一个**东西（声明支持的 Emby 版本），别混。
