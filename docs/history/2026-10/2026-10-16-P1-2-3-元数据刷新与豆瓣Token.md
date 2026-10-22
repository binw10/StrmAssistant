# P1-2 / P1-3 实施记录 · 元数据刷新规则补齐 + 豆瓣 Access Token

> **日期**：2026-10-16　**状态**：✅ 已完成（构建 0 错误）
> **对应**：`PRO功能移植方案-P0P1P2.md` 的 P1-2、P1-3
> **参考**：PRO 反编译 `StrmAssistant.Options.Metadata/MetadataRefreshOptions.cs`、
> `StrmAssistant.ScheduledTask/RefreshPersonTask.cs`、`strings.txt`（Douban API 端点明文）

---

## P1-2 · 元数据刷新规则多选化

### 一、交付内容

| 枚举 | 新增成员 | PRO 对应 | 语义 |
|---|---|---|---|
| `EpisodeRefreshOption` | `DefaultSeasonName` | `MetadataRefreshOptions.EpisodeRefreshOption.DefaultSeasonName` | 季名为默认名（“Season 1”/“第 1 季”）时判为需刷新 |
| `RefreshPersonOption` | `DefaultImageRefresh` | `PersonRefreshOption.DefaultImageRefresh` | 保守图像跳过：仅当已有主图且非强制下载/非本地图时跳过 |
| `RefreshPersonOption` | `FullImageRefresh` | `PersonRefreshOption.FullImageRefresh` | 强制全量刷新人物图像（永不跳过） |

### 二、实现要点

#### 2.1 `DefaultSeasonName`（剧集刷新）
- **判定器**：`Common/LanguageUtility.cs` 新增
  `DefaultEnglishSeasonNameRegex = ^(Season|Series)\s*\d+$`、
  `DefaultChineseSeasonNameRegex = ^第\s*\d+\s*季$`、
  `DefaultJapaneseSeasonNameRegex`，并提供 `IsDefaultSeasonName()` 聚合判定（三语任一命中）。
- **接入点**：`Common/LibraryApi.cs` 的 `EpisodeNeedsRefresh`，与既有 `DefaultEpisodeName` 判定并列：
  ```csharp
  (episodeRefreshOptions.Contains(EpisodeRefreshOption.DefaultSeasonName) &&
   LanguageUtility.IsDefaultSeasonName(item.Season?.Name))
  ```

#### 2.2 `DefaultImageRefresh` / `FullImageRefresh`（人物刷新）
- **反编译破译**：PRO `RefreshPersonTask.cs` 的控制流扁平化代码可还原为三分支（case 14/21/23/17）：
  ```
  if (FullImageRefresh)        imageRefreshSkip = false;                  // 永不跳过
  else if (!DefaultImageRefresh) imageRefreshSkip = true;                 // 默认：总是跳过
  else { var img = GetImageInfo(Primary,0);
         imageRefreshSkip = img != null && (!DownloadImagesInAdvance || img.IsLocalFile); }
  ```
- **保守落地**：社区版**保留原有历史行为作为基线**（`FullRefresh`/`DownloadImagesInAdvance`/30 天组合），
  仅把两个新开关叠加其上，优先级 `Full > Default > 历史行为`，确保老用户无回归。
- **接入点**：`ScheduledTask/RefreshPersonTask.cs` 的 `imageRefreshSkip` 判定改为显式三分支。

### 三、资源键（4 文件各 3 键）

| 键 | en | zh | zh-hant |
|---|---|---|---|
| `EpisodeRefreshOption_DefaultSeasonName_Default_Season_Name` | Default Season Name | 默认季标题 | 預設季標題 |
| `RefreshPersonOption_DefaultImageRefresh_DefaultImageRefresh` | Default image refresh | 默认图像刷新 | 預設圖像刷新 |
| `RefreshPersonOption_FullImageRefresh_FullImageRefresh` | Full image refresh | 全量图像刷新 | 全量圖像刷新 |

---

## P1-3 · 豆瓣 Access Token（开放平台 V2 API）

### 一、背景

社区版原有 `DoubanAssist` 走 **movie.douban.com 网页刮削**，易触发风控（`sec.douban.com`），
需配置 `DoubanCookie` 绕过。PRO 新增 `DoubanAccessToken`，改走**豆瓣开放平台 V2 API**。

### 二、端点来源（PRO `strings.txt` 明文还原）

| 项 | 值 |
|---|---|
| V2 基址 | `https://api.douban.com/v2` |
| 电影 | `/movie/{id}` |
| 剧集 | `/tv/{id}` |
| 鉴权头 | `Authorization: Bearer <token>` |
| 错误串 | `rate_limit` / `invalid_access_token` / `access_token_has_expired` / `user_is_locked` |

> 另有 frodo 移动端端点 `https://frodo.douban.com/api/v2`（`bid=J9zb1zA5sJc`，
> 对应 PRO `DoubanClient` 开关），属 App 私有签名流，本次**未移植**（成本高、收益低、易失效）。

### 三、实现要点

- **新增配置项**：`MetadataEnhanceOptions.DoubanAccessToken`（string，`VisibleCondition(EnableDoubanAssist)`）。
- **请求层**：`Provider/Douban/DoubanAssist.cs` 新增
  `FetchSubjectViaApiAsync()` → `TryFetchV2Async()`，用 `HttpRequestOptions` 带 `Authorization: Bearer`，
  解析为 `DoubanV2Subject`（`intro`/`rating.value`/`rating.count`/`genres`/`countries`/`languages`）。
- **回退链**：`FetchSubjectAsync` 先试 V2 API → 失败/未配置 → 回退原网页刮削（含风控判定与熔断）。
  即：**配置 Token 更稳，不配 Token 行为完全不变**。
- **日志**：`MetadataEnhanceOptionsStore` 新增 `DoubanAccessToken is set to CONFIGURED/EMPTY`。

### 四、资源键（4 文件各 2 键）

| 键 | en | zh | zh-hant |
|---|---|---|---|
| `MetadataEnhanceOptions_DoubanAccessToken_Douban_Access_Token` | Douban Access Token (Optional) | 豆瓣 Access Token（可选） | 豆瓣 Access Token（可選） |
| `MetadataEnhanceOptions_DoubanAccessToken_Used_to_assist_in_retrieving_Douban_info__Default_is_BLANK_` | 见 resx | 见 resx | 见 resx |

---

## 变更文件清单

```
StrmAssistant/Options/MetadataEnhanceOptions.cs          (+DefaultSeasonName/+DefaultImageRefresh/+FullImageRefresh/+DoubanAccessToken)
StrmAssistant/Common/LanguageUtility.cs                  (+季名正则与判定器)
StrmAssistant/Common/LibraryApi.cs                       (+DefaultSeasonName 接入 EpisodeNeedsRefresh)
StrmAssistant/ScheduledTask/RefreshPersonTask.cs         (imageRefreshSkip 三分支)
StrmAssistant/Options/Store/MetadataEnhanceOptionsStore.cs (+DoubanAccessToken 日志)
StrmAssistant/Provider/Douban/DoubanAssist.cs            (+V2 API 取数与回退)
StrmAssistant/Properties/Resources*.{resx,Designer.cs}   (+5 键 × 4 文件)
```

## 验证

- `dotnet build StrmAssistant.sln -c Release` → **0 错误**（1 个既有警告，位于 `SubtitleApi.cs`，与本次无关）。
