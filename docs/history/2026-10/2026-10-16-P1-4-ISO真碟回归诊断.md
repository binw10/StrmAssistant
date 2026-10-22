# P1-4 实施记录 · ISO 真碟回归测试

> **日期**：2026-10-16　**状态**：🔍 已完成诊断，等待用户决策
> **对应**：`PRO功能移植方案-P0P1P2.md` 的 P1-4
> **样本**：Emby 库内条目 `杜比视界测试：双层 FEL.iso`（GUID 见下），170MB / UDF-only 蓝光 ISO

---

## 一、测试环境

| 项 | 值 |
|---|---|
| Emby | `D:\下载\embyserver-win-x64-4.10.1.0`，v4.10.1.0，端口 8096 |
| 插件 | StrmAssistant（P1-2/P1-3 版，md5 `1115248...`），`IsoExtraction Status=Initialized` |
| 样本条目 | `杜比视界测试：双层 FEL.iso`（ItemId 253） |
| 条目 Path | `D:\视频\小号\杜比视界测试：双层 FEL.iso.strm` |
| MediaSource.Path | `http://172.18.70.52:8090/api/strm?...file_name=...FEL.iso` |
| Protocol / Container | `Http` / `strm` |
| 内置 ffprobe | `system\ffprobe.exe` |
| 自定义 ffprobe | `programdata\config\bin\ffprobe.exe`（**9.0.2**，`--enable-libbluray --enable-libdvdread --enable-libdvdnav`）|

### 样本 ISO 结构（HTTP Range 直读 CDN 验证）
- 文件头 @0：全 0（标准 ISO 引导区）
- VRS @32768：`BEA01` → **UDF 文件系统**（`DiscUtils.Udf.UdfReader` 可读）
- 大小：`178,323,456` 字节；经 115 CDN 中转（strm 代理返回 HTML `<a href="...">Found</a>` 跳转）

---

## 二、回归测试结果（关键发现）

### 🔴 发现 1：ISO 条目从未被成功探测

Emby API 查询该条目：
```
Container: None
RunTimeTicks: None
MediaSources[0]: { Path: http://...FEL.iso, Container: strm, Protocol: Http }
```
**无容器、无时长、无媒体流** → 说明 Emby **从未成功 ffprobe 过这个 ISO**。

### 🔴 发现 2：刷新流程完全不触发 ISO 处理

手动触发 `POST /emby/Items/253/Refresh?MetadataRefreshMode=FullRefresh` 后，日志（10:30:07→13）：
```
RefreshItem Start: 253 Movie D:\视频\小号\...FEL.iso.strm
  → TheMovieDb / OMDb / TVDb / Douban（仅元数据）
RefreshItem Complete: 253 Movie ...
DoubanAssist - No douban match
```
**全程 0 条** `IsoExtraction` / `UdfTitleResolver` / `ffprobe` / `MediaProbe` / mount 日志。
→ 刷新只做**元数据刮削**，**根本不探测媒体**（strm 的媒体解析是播放时惰性触发的）。

### 🔴 发现 3：既使开启开关，`IsoExtraction` 对本样本也**不可能命中**

`IsoExtraction.GetMediaInfoInternalPrefix` 依赖 `IsoMounter.TryNormalizeIsoPath`，而后者要求：
```csharp
if (candidate.Contains("://")) return false;     // ← HTTP URL 直接出局
if (!File.Exists(candidate)) return false;       // ← 还要求本地 .iso 文件存在
```
本样本的媒体路径是 **HTTP URL**（`Protocol=Http`），**永远无法通过**这两个判定。
→ 现有 ISO 提取逻辑只覆盖「**本地 .iso 文件**」，**不覆盖 strm 代理的 ISO**。

### 🔴 发现 4：`EnableIsoExtraction = False`（当前配置）

`StrmAssistant_MediaInfoExtractOptions.json` 中该项为 `False`，补丁加载但**未生效**。

---

## 二·补：ffprobe 直读本地 ISO 实测（重要发现）

用 **Emby 内置** `system\ffprobe.exe` 直接读本地 ISO 文件（仅前 30MB，经 115 CDN Range 下载）：
```
format_name=mpegts, nb_streams=2, bit_rate=55978133
stream0: hevc Main10 3840x2160 yuv420p10le, smpte2084/bt2020, codec_tag=HDMV
stream1: hevc Main10 1920x1080 yuv420p10le, smpte2084/bt2020, codec_tag=HDMV
```

**两个结论**：
1. ✅ **ffprobe 本身能读 ISO**（哪怕内置版）：它按 mpegts 扫到 HDMV 流，能出画面/编码/色彩。
   → 说明问题**不在 ffprobe 能力**，而在 **Emby 从未把 ISO 交给 ffprobe 探测**（因源是 HTTP strm）。
2. ⚠️ **本样本是 Dolby Vision Profile 7 双层（FEL）**：BL 4K + EL 1080p 两条 HEVC 流。
   当前 ffprobe 未合并为 DV profile（把 EL 当独立流），这正是 **P2-2「双层杜比 P7」** 要解决的点。

---

## 三、根因总结

社区版 ISO 能力的设计边界是「**本地 ISO 文件**」，两条通路：
1. **Windows**：`Mount-DiskImage` 挂载 → 卷上定位正片 → ffprobe 挂载路径
2. **Linux/Docker**：进程内 UDF 选片（仅日志）→ `CustomProbePath` 自定义 ffprobe 直读 `.iso`

而本样本是**第三种形态**：`movie.strm → HTTP(115 CDN) → .iso 字节流`。
该形态下：
- Emby 视其为 **Http 媒体源**，非本地文件；
- 刷新阶段不探测媒体；
- 播放阶段 ffprobe 拿到的输入是 **HTTP URL**，Emby 内置 ffprobe **不支持 http 输入里的 ISO/UDF 解析**（需 libbluray 才能读 `bluray:` 协议），故无法得到媒体信息。

> 这与 PRO 的差距正好落在 docs 已标注的 P2 项：**Pro 用 `bluray:"{0}"` + `--enable-libbluray` 的自定义 ffmpeg/ffprobe 直读**（见 PRO `strings.txt` 第 2071、2075、2419、2426、2437 行：`$1$2,bluray`、`bluray:"{0}"`、`--enable-libbluray`、`Custom ffprobe supports bluray input protocol`），且 PRO 有 `ExtractBlurayDisc` 类专门处理 `.iso.strm`、`OpenRead`、`PLAYLIST/*.mpls`、`CLIPINF/*.clpi`、`STREAM/*.m2ts`、`*.ssif`。

---

## 四、结论与建议

### 结论
**P1-4 回归测试暴露了一个真实能力缺口**：社区版 ISO 提取**不覆盖 strm/HTTP 形态的 ISO**，而这正是本项目（strm 直链 + 115 网盘）最主流的用法。
本样本（DV 双层 FEL ISO，经 strm 代理）当前**完全无法提取媒体信息**，与 PRO 存在代差。

### 建议（三选一，需用户决策）

| 方案 | 做法 | 成本 | 说明 |
|---|---|---|---|
| **A. 本地 ISO 先验证**（推荐先做） | 把样本 ISO **下载为本地文件**（去掉 strm），开 `EnableIsoExtraction`，验证挂载+MPLS 选片+正片探测链路 | 低 | 先确认**已移植的本地链路**是否正确；下载受限于代理带宽（~1MB/12s） |
| **B. 支持 strm/HTTP ISO**（对齐 PRO 能力） | 新增：识别 `Protocol=Http 且 file_name 以 .iso 结尾` → 用带 libbluray 的自定义 ffprobe 以 `bluray:"<url>"` 或先拉取头部 → 解析 UDF/MPLS | **中高** | 本质是 P2-4「http/smb 内置」+ P2-1「MPLS 语言/章节」的合并子集；需改 `IsoMounter`/`IsoExtraction` 命中条件 |
| **C. 暂时搁置** | 记录为已知限制，先推进 P2-1（本地 ISO 的 MPLS 语言/章节，Wiki 第 2 条） | 低 | 本地 ISO 用户受益，strm 用户不受益 |

### 我的推荐
先做 **A**（低成本验证已移植链路是否 OK），再评估 **B** 是否值得（取决于你是否主要用 strm 看 ISO）。
若你**主要就是 strm 看 ISO**，那 B 的价值高于 P2-1，应当**把 B 提前**。

### ✅ 决策结果（2026-10-16）
用户确认**主要用 strm 看 ISO** → **采纳方案 B**，并已实施完成（ISO-1~4）。实现要点见下节。

---

## 六、方案 B 实施记录（2026-10-16 完成）

**目标**：让 strm/HTTP 形态的 ISO 也能探测出媒体信息，对齐 PRO 的 `bluray:` 协议做法。

| 步 | 改动 | 文件 |
|---|---|---|
| ISO-1 | 新增 `TryNormalizeRemoteIso()`：识别 http/https 且以 `.iso` 结尾的 URL，放行 `://`、不做 `File.Exists` 校验 | `Common/IsoMounter.cs` |
| ISO-2 | 重写 `GetMediaInfoInternalPrefix`：远程优先 → 本地次之；在能力满足时把 `inputPath` 改写为 `bluray:"{0}"` | `Mod/IsoExtraction.cs` |
| ISO-3 | 新增能力位 `ProbeSupportsBluray`：`SelfCheck` 检出 `--enable-libbluray` 时缓存；供 ISO-2 门控 | `Common/FfmpegPathOverride.cs` |
| ISO-4 | 更新 `EnableIsoExtraction`/`CustomProbePath` 文案（三语），说明支持 strm/http ISO | `Properties/Resources*.resx` |

**安全设计（延续红线）**：
- 仅当 `ProbeSupportsBluray == true` 时才改写输入；否则**保守放行原路径**（不影响常规条目）。
- **始终不改 `MediaSourceInfo.Path`** → 播放路径与条目身份不受影响。
- `EnableIsoExtraction` 默认 OFF，用户显式开启才生效。

**构建部署**：`dotnet build -c Release` 0 错误；DLL md5 `2db12f628d2d9048ef25a706a25d8e05`，两个 plugins 目录一致。

**验证步骤（待用户操作）**：
1. 重启 Emby（加载新版 DLL）。
2. 配置 `自定义 Probe 路径` = 带 libbluray 的 ffprobe（如 `programdata\config\bin\ffprobe.exe` 9.0.2）。
3. 开启 `EnableIsoExtraction`。
4. 对样本 253 触发媒体信息提取，观察日志：
   - `FfmpegPathOverride - ProbePath supports bluray input protocol`（能力位生效）
   - `IsoExtraction - remote iso probing via bluray protocol -> bluray:"http://..."`（命中改写）
5. 条目 `Container` / `RunTimeTicks` / 媒体流 不再为 `None` 即成功。

**另注**：Emby 4.10.1.0 自带官方 `BlurayMounter.dll`/`DvdMounter.dll`（hook `IFileSystem.OpenRead` 挂载盘镜像，与 PRO 机制同源），是 ISO 读盘的另一条可用通路，与本方案的 `bluray:` 协议路线互补，后续可按需评估。

**后续**：ISO 真碟回归（多分支/多 m2ts 选片正确性）→ MPLS 语言/章节提取 → 双层杜比 P7 → ISO 截图。

---

## 七、最终方案（2026-10-16 晚）—— 端到端打通 ✅

### 7.1 推翻 `bluray:` 路线

实测 Emby 内置 `system/ffprobe.exe`（5.1-emby）**编译时带 `--enable-libbluray`**，但该协议在 **Windows 上读不了 UDF**：

```text
bluray:"D:\...\FEL2.iso"   → failed opening UDF image / bd_open() failed
bluray:"E:/"（已挂载盘）    → 同样失败
bluray:"http://..."        → 同样失败
```

而 **ffprobe 直读裸 `.iso`（本地路径或 http URL）** 反而能当 mpegts 成功解析出全部流（4K BL + 1080p EL）。
→ **`bluray:` 路线彻底放弃**，`FfmpegPathOverride.ProbeSupportsBluray` 能力位不再是关键路径。

### 7.2 真正根因：Emby 4.10 的两道硬闸门（反编译 `Emby.Providers.dll` 得到）

```csharp
// Emby.Providers.MediaInfo.FFProbeProvider.cs
public static bool IsSupported(string path, MediaProtocol protocol)
{
    if (string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase))
        return false;                 // ← 硬编码拦截：任何 .iso 都不给探测
    return true;
}
```

```csharp
// Emby.Providers.MediaInfo.BaseMediaInfoProber.cs :: Probe()
using (IMediaMount mediaMount = await IsoManager.Mount(src.ToFileSystemMetadata(), src.Container, ct))
{
    if (mediaMount != null) { path = mediaMount.MountedPathInfo.FullName; protocol = mediaMount.MountedProtocol; }
    if (FFProbeProvider.IsSupported(path, protocol) && FFProbeProvider.SupportsProbing(mediaSource))
        mediaInfoResult = await MediaProbeManager.GetMediaInfo(...);   // ← 探测入口
}
```

- 内置 `BlurayMounter` / `DvdMounter` 的 `CanMount(…)` **只认目录**（`fileInfo.IsDirectory` 且 container = `bluray`/`dvd`），
  对 `.iso` **文件**恒返回 `false` → `path` 保持 `.iso` → 撞上 `IsSupported` 的 `.iso` 拦截 → **整个探测被跳过**。
- 结论：**Emby 从不探测 `.iso`，与 ffprobe 能力无关**。这就是此前 "无 ffprobe 调用、无 IsoExtraction 日志" 的根因。

### 7.3 最终实现（两处 Harmony Prefix，均在 `Mod/IsoExtraction.cs`）

| 步 |  hooked 目标 | 作用 |
|---|---|---|
| ① | `FFProbeProvider.IsSupported(string, MediaProtocol)` | 选项开启且 `IsoMounter.LooksLikeIsoPath(path)` → `__result=true; return false`（**解锁闸门**） |
| ② | `MediaProbeManager.GetMediaInfoInternal`(7 参) | 把 `inputPath`（已被包成 `file:"..."` 或 `"http://..."`）替换为 ffprobe 可稳定读取的本地路径 |

- **本地 `.iso`** → Windows 挂载取正片；失败退回进程内 UDF（不改输入，交由 ffprobe 直读裸 ISO）。
- **远程 http(s) ISO** → `IsoMounter.GetRemoteProbePath()`：HTTP Range 拉 8MB 头部窗口落盘为临时文件再探测。
- 红线不变：**始终不改 `MediaSourceInfo.Path`**；取不到可用路径时保守放行；选项默认 OFF。

### 7.4 `IsoMounter` 关键修正

1. **`LooksLikeIsoPath(raw)`**（新）：宽松判定，必须兼容 **strm 代理 URL 把文件名放在 query 末尾** 的形式
   （`…/api/strm?…&file_name=xxx.iso`）—— 第一版按 `AbsolutePath` 判断导致闸门解锁失败，已修正为
   「整串结尾 `.iso`」或「剥离 query 后 `.iso`」或「URL 解码后再判」三条并行。
2. **`TryNormalizeRemoteIso`**：改以 `uri.AbsolutePath` 或整串结尾判 `.iso`，容忍 query。
3. **`DownloadRange`** 重写：
   - **读取空闲看门狗**：连续 `ReadIdleTimeoutMs = 20s` 无新数据即中止（旧实现 `src.Read()` 阻塞 →
     `HttpClient.Timeout` 根本不触发 → 任务永久卡在 `Cancelling`）。
   - **整体上限** `RemoteDownloadTimeoutMs = 180s`。
   - **接受部分内容**：`MinAcceptableBytes = 1MB`（实测 **3MB 窗口已足够** ffprobe 识别全部流）。
   - 目标文件 `FileShare.Read` 打开。

### 7.5 实测证据（Emby 4.10.1.0 在线，真实条目）

| 条目 | 形态 | 改前 | 改后 |
|---|---|---|---|
| 424 `杜比视界测试：双层 FEL2` | 本地 `.iso` | Container=iso, RT=None, 0 流 | **Container=iso, RT=306666670, 2 流**（hevc 3840×2160 + 1920×1080） |
| 253 `杜比视界测试：双层 FEL.iso` | `.strm`→HTTP ISO | Container=None, RT=None, 0 流 | **Container=mpegts, RT=10416670, 2 流**（hevc 3840×2160 + 1920×1080） |

即 Dolby Vision **Profile 7 双层**结构（BL 4K + EL 1080p）被正确识别。再次运行提取任务 → `Number of items: 0`（已持久化）。

**部署 md5**：`f1bf714dbc7c64dc29eb37ae6519b08c`。

### 7.6 遗留

- `FfmpegPathOverride.Apply` 在 Emby 4.10 上 `FfmpegManager.FfmpegConfiguration == null` → 自定义 ffprobe/ffmpeg 覆盖未生效；
  与 ISO 无关（现已不需要），如需启用可后续单独排查。
- 下一步：MPLS 语言/章节提取（Wiki#2）→ DV P7 判定 → ISO 截图。

---

## 五、附：可直接复现的验证命令

```bash
# 1) 查看条目媒体信息（应为 Container=None）
TOKEN=<admin_token>
curl -s -H "X-Emby-Token: $TOKEN" \
  "http://localhost:8096/emby/Items?SearchTerm=杜比视界&Recursive=true&IncludeItemTypes=Movie&Fields=Path,MediaSources,Container"

# 2) 触发刷新
curl -s -X POST -H "X-Emby-Token: $TOKEN" \
  "http://localhost:8096/emby/Items/253/Refresh?MetadataRefreshMode=FullRefresh"

# 3) 观察日志（刷新期间应无任何 IsoExtraction/probe 行）
grep -iE "iso|probe|playlist|m2ts|mount" \
  "D:/下载/embyserver-win-x64-4.10.1.0/programdata/logs/embyserver.txt" | tail

# 4) 自定义 ffprobe 直读本地 ISO（验证 libbluray 通路）
"programdata/config/bin/ffprobe.exe" -v error -show_format -show_streams \
  "D:/path/to/local/DV-FEL.iso"
```
