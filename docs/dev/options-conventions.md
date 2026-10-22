# 选项增删与迁移约定

设置项是这个插件最容易出问题的地方 —— 加一个字段要同时改 5 个地方，漏一个就是设置页 500。
这份文档把流程固化下来。

## 加一个开关：完整步骤

以「启用 ISO 提取」为例。

**1. 在对应的 `XxxOptions` 里加属性**

```csharp
[DisplayNameL("MediaInfoExtractOptions_EnableIsoExtraction_Enable_ISO_Extraction", typeof(Resources))]
[DescriptionL("MediaInfoExtractOptions_EnableIsoExtraction_Description", typeof(Resources))]
[Required]
[VisibleCondition(nameof(SomeParentFlag), SimpleCondition.IsTrue)]   // 可选
public bool EnableIsoExtraction { get; set; } = false;
```

**2. 三个 resx 各加两条**（DisplayName + Description）

`Properties/Resources.resx`（英文）、`Resources.zh.resx`、`Resources.zh-hant.resx`。

**3. `Resources.Designer.cs` 不用手改**

构建时 `tools/check_resx_keys.py --fix` 会自动补齐（挂 `BeforeTargets="CoreCompile"`）。
正常输出 `[OK] resx keys consistent`，补了东西会打 `[FIX] auto-added N missing property(ies)`。

> 🔴 为什么不能靠 VS：`PublicResXFileCodeGenerator` 是**设计期**生成器，`dotnet build` 不运行它。
> Designer.cs 只是一个被 `Compile` 的普通文件。少了属性 → `LocalizableString` 反射取不到 →
> 设置页 GET 直接 **500**，栈顶是 `EditorHelpers.GetSelectOptionsFromEnumList`。

**4. 接线**

在 `XxxOptionsStore` 的变更处理里响应开关（`changedProperties.Contains(nameof(...))` → Patch/Unpatch）。

**5. 验证**

```
GET /emby/UI/View?PageId=63c322:<ViewName>&ClientLocale=zh-cn   → 200
```
再用 JSON 上下文确认文案挂在**正确的选项**上（不要只看页面里有没有那句话）。

## 改单位 / 改语义：三段式迁移

典型场景：`CatchupMinWaitMinutes`（分钟，0–120）→ `CatchupMinWaitSeconds`（秒，0–7200）。

**1. 保留旧字段**，标 `[Browsable(false)]`，保留旧默认值：

```csharp
[Browsable(false)]
public int CatchupMinWaitMinutes { get; set; } = 0;

public int CatchupMinWaitSeconds { get; set; } = 0;

public int EffectiveCatchupMinWaitSeconds =>
    CatchupMinWaitSeconds > 0 ? CatchupMinWaitSeconds : Math.Max(0, CatchupMinWaitMinutes) * 60;
```

**2. 在 `Plugin.Initialize` 里一次性换算并落盘**：

```csharp
if (taskConcurrency.CatchupMinWaitMinutes > 0 && taskConcurrency.CatchupMinWaitSeconds == 0)
{
    taskConcurrency.CatchupMinWaitSeconds = taskConcurrency.CatchupMinWaitMinutes * 60;
    taskConcurrency.CatchupMinWaitMinutes = 0;
    MainOptionsStore.SavePluginOptionsSuppress();
    Logger.Info($"migrated CatchupMinWaitMinutes to CatchupMinWaitSeconds ({...} s)");
}
```

> 🔴 **不能放在 `XxxOptions.Initialize()` 里**。`Initialize()` 只在**设置页加载时**被调用
> （`HomePageView` → `GeneralOptions.Initialize()`），启动阶段不执行。放那里会内存值对、JSON 不落盘，
> 用户下次打开设置页看到的是 0。

**3. 检查消费方的容差**

改成秒级后，原来 `wait > TimeSpan.FromSeconds(5)` 的容差会把小值完全吃掉 —— 一并改成 1 秒。
**不改容差的话，改秒级等于白改。**

## 拍平/拆组这类结构迁移

模式：读旧结构 → 填新结构 → 落盘 → 留备份 → 用 flag 文件防重复。

```csharp
// 旧容器保留为 [Browsable(false)] 且【没有初始化器】
// JSON 里没有该节点时保持 null —— 这就是"是否需要迁移"的判断依据
[Browsable(false)]
public GeneralOptions GeneralOptions { get; set; }

if (options.GeneralOptions != null)
{
    options.TaskConcurrency = options.GeneralOptions.TaskConcurrency ?? new TaskConcurrencyOptions();
    options.GeneralOptions = null;
    SavePluginOptionsSuppress();
}
```

拆组迁移用 flag 文件防重复：`..._ui_groups_v2.flag`，改之前备份 `*.ui_groups.bak`。

## 五个已知陷阱

**1. resx 的 `<value>` 里不能有裸 `<`**

```xml
❌ <value>清空 <root>\cache 目录</value>
✅ <value>清空 &lt;root&gt;\cache 目录</value>
```

批量替换时**别把文档根标签 `<root>` 也换掉**。替换完跑一遍 `ET.parse()` 校验 XML。

**2. 批量 sed 会误伤前缀相同的字段**

```
sed 's/\.GeneralOptions\.Memory/.Memory/g'
   → 也会改到 legacy.GeneralOptions.MemoryAutoRestartThresholdMB（Memory 是它的前缀）
```

批量替换后**必须编译 + 看 diff**，不能只看替换数量。

**3. 正则替换时 `\.` 不要写进替换串**

pattern 里的 `\.` 是转义，替换串里写 `\.` 会原样落进代码，编译报 `CS1056`。

**4. `[VisibleCondition]` / `[EnabledCondition]` 按同级路径解析**

引用的 helper 必须与消费方**同组**，否则前端 `getValue` 拿到 `undefined`，控件莫名隐藏。
`[Browsable(false)]` 的 helper 不进 `EditorItems` 但**会进 `Object` JSON**，条件照样能解析。

**5. 嵌套 `Initialize()` / `Validate()` 不会自动调用**

父类必须手工委派（`ValidateChildGroup`）。拍平后 `PluginOptions` 加了 `Validate` 覆写来委派 `TaskConcurrency`。

## 选项文件是怎么拆的

`PluginOptionsStore` 只是主 Store，其余按 Tab 拆成独立 Store，各自一个 JSON：

| Store | 文件 | 覆盖 |
|---|---|---|
| `PluginOptionsStore` | `StrmAssistant.json` | 基础设置（任务并发 / 内存 / 中文搜索 / 运行状态 / 关于） |
| `MediaInfoExtractOptionsStore` | `StrmAssistant_MediaInfoExtractOptions.json` | 媒体信息提取 + ISO |
| `MetadataEnhanceOptionsStore` | `StrmAssistant_MetadataEnhanceOptions.json` | 元数据增强 + 缓存 |
| `ExperienceEnhanceOptionsStore` | `StrmAssistant_ExperienceEnhanceOptions.json` | 高级功能（含迁移逻辑） |
| `IntroSkipOptionsStore` | `StrmAssistant_IntroSkipOptions.json` | 片头片尾 |
| `NetworkProxyOptionsStore` | `StrmAssistant_NetworkProxyOptions.json` | 网络代理 |

新选项放哪个 Store，取决于它出现在哪个 Tab —— **不要为了省事都塞进主 Store**。
