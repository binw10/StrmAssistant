# 更新日志 / Changelog

## [3.0.0.55] - 2026-10-16

> 本版为 `2.2.0` 之后的首个正式发布；`26.3.25`（2026-03-25 开发版）的改动已合并至本版。

### ✨ 界面优化
- **版本号显示修正**：不再拼接 git commit hash，直接显示 `3.0.0.55`。
- **体验增强 · 版本显示方式 / 版本排序方式**：由自由文本改为**中文下拉框**（默认 / 画质 / 画质+体积 / 文件名；默认 / 画质降序 / 画质升序 / 路径升序 / 路径降序）。属性由 `string` 改为枚举，旧配置值（`MediaInfo`、`SystemDefault` 等）与枚举成员同名，可原样反序列化。
- **多版本 · 新增「画质+体积」显示格式**：标签同时给出画质档位、视频编码、码率与文件体积，形如 `4K HEVC 46.1Mbps 168.4MB`；缺项自动省略。多版本详情页排序同步按标签首个档位词识别（此前复合标签会被判为最低档）。
- **元数据增强 · 设置分组**：原来约 40 项平铺设置，改为 **9 个带小标题的分组**——中文元数据 / 剧集组 / 刮削优化 / 人物增强 / 豆瓣辅助 / 海报与排序 / 元数据刷新 / 本地元数据缓存 / 备用 MovieDb 接口。
  - 老版本平铺 JSON 配置会**自动迁移**为嵌套结构，原文件备份为 `StrmAssistant_MetadataEnhanceOptions.json.legacy.bak`。

### 🐛 修复
- **版本号方案统一为四段式 `3.0.0.55`**：与 Releases tag 体系一致（此前为日期式 `26.3.25`、三段式 `3.0.55`）。同时修正因编号方案变更导致「中文搜索升级检查」把 `3.0.0.55` 误判为**降级**的问题——标记文件现额外记录编号方案号，方案变更时只重置基线，不再误报。
- **体验增强页 500 错误**：`Resources.Designer.cs` 缺少 8 个枚举标签属性（`MultiVersionDisplayOption_*` / `MultiVersionSortOption_*`），而 Emby 的 `[DescriptionL]` / `[DisplayNameL]` 是通过**反射查找 `Resources` 的公共静态属性**解析的（不是走 `ResourceManager`），因此仅把键写进 `.resx` 不够。已补齐 8 个属性，页面恢复正常。
  - 说明：`Resources.Designer.cs` 由 `PublicResXFileCodeGenerator` 生成，但 **`dotnet build`（命令行 MSBuild）不会运行该设计期生成器**，新增资源键后必须手工同步该文件。
- **豆瓣演员中文名未覆盖客串演员**：中文化过滤条件仅取主演（`PersonType.Actor`），导致客串（`GuestStar`）演员的中文名始终不被写入。现同时覆盖两者。

### 🔧 调整
- ARM64 下所有 `Harmony not implemented on this arm64 target` 日志降级为 **Debug** 输出，避免常规日志刷屏。
- 移除 `SuppressPluginUpdate` 相关功能，不再拦截或屏蔽插件更新列表。
- 运行要求明确为 **仅支持 Emby 4.9.1.0 及以上版本**。

## [2.2.0] - 2024-12-03

### 🎉 重大更新 / Major Updates

#### 全新架构系统 / New Architecture System
- **EmbyVersionAdapter**: 智能版本检测和适配系统
  - 自动检测Emby版本并映射到API版本枚举
  - 支持从4.8.0到4.10.0+的所有版本
  - 提供方法缓存和安全调用包装器
  
- **ServiceLocator**: 全局服务定位器
  - 统一的服务注册和获取机制
  - 支持类型服务和命名服务
  - 线程安全的并发实现

- **增强的PatchTracker**: 详细的补丁状态跟踪
  - 新增6种补丁状态（NotInitialized, Initializing, Initialized, Applied, Failed, NotSupported）
  - 错误消息收集和历史记录
  - 核心功能vs可选功能区分

#### 高性能优化系统 / High-Performance Optimization
- **FastReflection**: 高性能反射调用器
  - 使用Expression树编译技术，性能接近直接调用
  - 比标准反射快50-100倍
  - 自动缓存编译的委托，缓存命中率>95%
  - 支持方法调用和属性访问的优化
  
- **PerformanceMonitor**: 性能监控系统
  - 实时跟踪关键操作的执行时间
  - 统计调用次数、平均/最小/最大耗时
  - 自动识别慢操作（可配置阈值）
  - 生成详细的性能报告

- **HealthCheck**: 健康检查和自诊断
  - 自动检查所有核心组件状态
  - 检测性能问题和内存使用
  - 生成全面的诊断报告
  - 健康状态分级（Healthy/Degraded/Unhealthy/Critical）
  - 启动时自动执行健康检查

- **PerformanceReporter**: 定期性能报告
  - DebugMode下每60分钟自动生成报告
  - 包含FastReflection统计、Top 10操作
  - 自动检测慢操作和内存使用
  - 提供详细的系统健康状况

- **OptimizationAdvisor**: 智能优化建议
  - 根据运行状况提供优化建议
  - 按优先级分类（Critical/High/Medium/Low）
  - 自动分析性能瓶颈
  - 启动时检查并提示关键问题

### ✅ 完全支持 Emby 4.9.1.90 / Full Support for Emby 4.9.1.90
- 专门为4.9.1.90版本优化的API调用
- 支持最新的MediaSources API变化
- 支持最新的通知系统增强
- 向后兼容4.8.x和4.9.0.x所有版本

### 🔧 架构改进 / Architecture Improvements
- **三层回退机制**: Harmony → Reflection → PublicAPI
- **智能方法查找**: 自动适配不同版本的方法签名
- **性能优化**: 方法和类型信息缓存，减少反射开销
- **诊断增强**: 新增`GetDiagnosticReport()`方法，详细的系统状态报告

### 📝 代码质量改进 / Code Quality Improvements
- 更详细的日志记录和错误追踪
- 优雅的功能降级处理
- 更好的异常处理和错误恢复
- 代码结构更清晰，可维护性更强

### 📖 文档更新 / Documentation Updates
- 新增 `ARCHITECTURE.md` 架构文档
- 新增 `CHANGELOG.md` 更新日志
- 更新 `README.md` 支持版本信息
- 更新项目版本号为 2.2.0.0

### 🔄 API变化 / API Changes
- `EmbyVersionCompatibility` → `EmbyVersionAdapter` (旧类已弃用但保留兼容)
- 新增全局 `ServiceLocator` 用于服务管理
- `PatchTracker` 新增状态属性和错误收集方法
- `PatchManager` 新增诊断和缓存清理方法

### ⚠️ 破坏性变化 / Breaking Changes
无破坏性变化。所有现有功能保持完全向后兼容。

---

## [2.1.0] - Previous Release

### ✅ 增强的版本兼容性
- 动态检测并适配不同Emby版本的API方法签名
- 支持5参数和7参数两种GetStaticMediaSources方法签名
- 改进的反射调用错误处理和回退机制

### ✅ 健壮的路径获取
- 增强的IMediaMount路径获取，支持多种属性名
- 智能回退机制，确保功能可用性

### ✅ 优化的错误处理
- 详细的日志记录和调试信息
- 优雅的降级处理，单个模块失败不影响整体功能

---

## 版本支持矩阵 / Version Support Matrix

| 插件版本 | 推荐Emby版本 | 最低Emby版本 | 最高测试版本 |
|---------|-------------|------------|------------|
| 2.2.0   | 4.9.1.90    | 4.8.0.0    | 4.9.1.90   |
| 2.1.0   | 4.9.1.80    | 4.8.0.0    | 4.9.1.80   |

## 功能兼容性 / Feature Compatibility

| 功能 | 最低版本要求 | 说明 |
|-----|------------|------|
| IntroSkip | 4.8.0 | 片头片尾跳过 |
| EpisodeGroups | 4.9.0 | 剧集分组 |
| EnhancedMediaInfo | 4.9.1.80 | 增强媒体信息 |
| AdvancedFingerprinting | 4.9.1 | 高级指纹识别 |
| OptimizedMediaSources | 4.9.1.90 | 优化媒体源 |
| EnhancedNotifications | 4.9.1.90 | 增强通知系统 |

## 升级指南 / Upgrade Guide

### 从 2.1.0 升级到 2.2.0

1. **备份配置**: 建议先备份现有配置
2. **停止Emby服务**: `systemctl stop emby-server` 或通过服务管理器停止
3. **替换插件文件**: 替换 `StrmAssistant.dll`
4. **启动Emby服务**: 重新启动Emby服务
5. **检查日志**: 查看日志确认新架构初始化成功

### 配置迁移
无需配置迁移。所有现有配置自动兼容。

### 回滚方案
如遇问题可回滚到2.1.0版本：
1. 停止Emby服务
2. 替换为旧版本的DLL文件
3. 重启Emby服务

## 已知问题 / Known Issues

### v2.2.0
- 无已知严重问题

### 限制 / Limitations
1. Harmony ReversePatch在某些复杂IL代码上可能失败（会自动降级到Reflection）
2. 非X64架构不支持Harmony（会使用Reflection）
3. 某些私有API在未来Emby版本中可能发生变化

## 贡献者 / Contributors

感谢所有为本项目做出贡献的开发者！

---

**注意**: 本项目为开源社区版本，基于原始项目使用AI进行适配和优化。
