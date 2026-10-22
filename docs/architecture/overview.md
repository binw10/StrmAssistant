# 架构总览

Emby 插件（`BasePlugin` + `IService` 装配）。核心思路是**不重写 Emby，只在关键方法上打 Harmony 补丁**，
把 strm 直链、中文分词、ISO 原盘这些 Emby 原生不支持的能力嫁接进去。

## 目录职责

| 目录 | 职责 | 备注 |
|---|---|---|
| `Mod/` | **补丁本体**，一个功能一个 `PatchBase` 派生类 | 40 个文件，其中约 37 个参与补丁 |
| `Common/` | 业务逻辑：队列、媒体信息、库查询、章节、指纹 | 补丁只做"拦截"，真正的活在这层 |
| `Options/` | 所有设置项（`EditableOptionsBase` 派生）与各 Store | 见下文「设置页结构」 |
| `Core/` | 基础设施：版本适配、内存清理、健康检查、性能探针、冲突检测 | 与 Emby 业务无关 |
| `ScheduledTask/` | 13 个定时任务 | 批量作业入口，也用于手工触发验证 |
| `IntroSkip/` | 播放会话跟踪（片头片尾） | 订阅 Emby 播放事件 |
| `Provider/` | 自定义 Metadata Provider（豆瓣、TMDB 剧集组） | |
| `Web/` | 14 个自定义 REST 端点 + 前端注入脚本 | 见 `architecture/api.md` 章节（合并在本目录） |
| `Tokenizer/` | 中文分词器原生库（`libsimple`）源码与三平台预编译产物 | 见 [chinese-search.md](chinese-search.md) |
| `Properties/` | 三语资源（`Resources.resx` / `.zh` / `.zh-hant`）与 `Resources.Designer.cs` | 🔴 见 [../dev/options-conventions.md](../dev/options-conventions.md) |

## 启动流程（`Plugin.Initialize`）

顺序有依赖关系，改动时要留意：

1. **`EnablePlugin` 总开关**：关闭时整段跳过 —— 不打补丁、不订阅事件、不初始化队列，定时任务与 Web 端点早退。
2. **`PatchManager.Initialize()`** → 应用全部 Harmony 补丁（当前 37 个），日志 `All 37 patches initialized successfully`。
   失败则回落反射方案（`Harmony Mod is not supported in this environment`）。
3. **`MemoryCleaner.ApplySettings`** → 按 `Memory.EnableMemoryCleanup` 启停清理器。
4. **各 Store 加载** → `MediaInfoExtractStore` / `MetadataEnhanceStore` / `ExperienceEnhanceStore` / `IntroSkipStore`。
5. **运行时组件** → `QueueManager.Initialize()`（追更队列）、`PlaySessionMonitor`（片头片尾）、`AutoMarkWatched`。
6. **配置迁移**（一次性，写完落盘，见 [../dev/options-conventions.md](../dev/options-conventions.md)）。
7. **`HealthCheck`** → 输出 `Startup Health Check: Healthy`，异常项会写 Warn。

> 🔴 **`XxxOptions.Initialize()` 只在设置页加载时被调用**，启动阶段不执行。
> 任何"启动时归一化并落盘"的逻辑必须写在 `Plugin.Initialize` 里，放 `Initialize()` 里会内存值对、JSON 不落盘。

## 设置页结构

6 个 Tab。主页面 id 是 `63c322:Settings`（**不是** `HomePageView`），子 Tab 用 `nameof(XxxPageView)`。

```
基础设置（63c322:Settings）
  ├─ 免责声明按钮 / 品牌组 / 启用插件 / 状态条（两个状态条默认隐藏）
  ├─ 任务并发
  ├─ 内存管理
  ├─ 中文搜索
  ├─ 运行状态与维护   ← 含重启服务器，按「危险操作居末」放最后
  └─ 关于             ← 版本链接 + 界面语言（auto / zh-CN / zh-Hant / en-US）
高级功能（ExperienceEnhancePageView）
  ├─ 多版本合并 → 多版本展示 → 播放与清理 → 通知
  ├─ 条目显示        ItemDisplayOptions
  ├─ 媒体库与排序    LibraryOrderOptions
  └─ 搜索与日志      SearchLogOptions
媒体信息提取 / 元数据增强 / 片头片尾 / 网络代理
```

排序逻辑：**条目级 → 库级 → 系统级**，影响面由小到大。别把「媒体库与排序」挪到「条目显示」前面，
前四组都是条目级，插库级会让层级来回跳。

### 三个容易踩的渲染规则

1. **resx 标题留空 ≠ 隐藏该组**，而是渲染成一个**没有标题的折叠块**。想去掉标题必须把那层容器拆掉
   （参考 `GeneralOptions` / `ModOptions` 的拍平迁移）。
2. **嵌套 `EditableOptionsBase` 属性 → 折叠组**，组标题取父属性的 `DisplayName`。
3. **`[VisibleCondition]` / `[EnabledCondition]` 按同级路径解析**，引用的 helper 必须与消费方**同组**，
   否则前端 `getValue` 拿到 `undefined`，控件会莫名隐藏。

### 界面语言

`AboutOptions.DefaultUICulture` 控制插件文案语言，取值 `auto` / `zh-CN` / `zh-Hant` / `en-US`。

实现方式是设置进程级的 `Resources.Culture`：

- `auto` → 设为 `null`，`ResourceManager` 回退到**请求线程**的 `CurrentUICulture`，即跟随客户端；
- 其余 → 固定语言。

因此**选项文案改完立即生效**（下一次请求就按新语言渲染），
但 **Tab 标题需要重启** —— 它在 `MainPageController` 构造时求值，随插件加载缓存。


## 数据落盘位置

| 内容 | 位置 |
|---|---|
| 插件选项 | `programdata/plugins/configurations/StrmAssistant*.json`（**按 Store 拆成多个文件**） |
| 媒体信息备份 | 与视频同目录的 `*.mediainfo.json`（可配置根目录） |
| TMDB 被动缓存 | `<MetadataCacheRootFolder>/cache`，用户 override 与 patches 在另外两个子目录 |
| 中文索引完成标记 | 见 [chinese-search.md](chinese-search.md) |

## 相关文档

- 补丁模型与清单 → [patching.md](patching.md)
- 中文搜索链路 → [chinese-search.md](chinese-search.md)
- ISO 链路 → [iso.md](iso.md)
- Emby 版本适配 → [emby-version.md](emby-version.md)
- 自定义端点清单 → [api.md](api.md)
