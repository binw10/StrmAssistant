# 实机验证手册

改完代码必须真跑一遍。这份手册把"怎么取凭证、怎么触发、看哪个日志、查哪张表"固化下来，
省得每次重新摸。

环境：Emby **4.10.1.0** @ `D:\下载\embyserver-win-x64-4.10.1.0\`

## 0. 前置

```bash
# 构建 + 部署（自动落到 plugins 目录）
dotnet build StrmAssistant/StrmAssistant.csproj -c Release

# 重启 Emby（必须带 -noautorunwebapp，否则后台进程会被回收）
cd "D:/下载/embyserver-win-x64-4.10.1.0/system"
./EmbyServer.exe "D:/下载/embyserver-win-x64-4.10.1.0/system/EmbyServer.dll" -noautorunwebapp

# 等约 95 秒
```

启动成功的判定（三条都要有）：

```
All 37 patches initialized successfully
Startup Health Check: Healthy
MemoryCleaner started - Cleanup every 5 minutes
```

## 1. 取 API Key

**API Key 会失效**（Emby 重启、用户登出后重建）。失效表现为 `401`。

```python
# programdata/data/authentication.db
select AccessToken, UserId, IsActive, AppName
from Tokens_2 order by DateCreatedInt desc limit 5
```

- 取最新那条、`IsActive=1`、`UserId=1`（管理员）。
- ⚠️ `UserId` 是 **int 不是 Guid**，按 Guid 解析会报 `'int' object is not subscriptable`。

```bash
TOKEN=<上一步取到的>
curl -s --noproxy '*' -o /dev/null -w "%{http_code}\n" \
  "http://127.0.0.1:8096/emby/System/Info?api_key=$TOKEN"
# 期望 200
```

> 本机代理会拦 `127.0.0.1`，curl 一律加 `--noproxy '*'`。

## 2. 触发手段（选错了就白等）

| 想验证什么 | 用什么 | 注意 |
|---|---|---|
| 插件设置页渲染 | `GET /emby/UI/View?PageId=63c322:<ViewName>&ClientLocale=zh-cn` | **必须用 `X-Emby-Token` 头**，`api_key=` 会 400 |
| 保存设置页 | `POST /emby/UI/Command`，`CommandId=PageSave` | **`Data` 必须是 JSON 字符串**（前端 stringify 了两次）。传嵌套对象 → 200 但整页落成默认值 |
| 普通条目刷新 | `POST /emby/Items/{id}/Refresh?metadataRefreshMode=FullRefresh` | |
| **`.strm` / ISO 探测** | `POST /emby/ScheduledTasks/Running/f84e5d989aa8a8b6ab1a3a8c0848faab` | 🔴 **`/Items/{id}/Refresh` 无效**：shortcut 条目会被 `BaseMediaInfoProber.Probe` 跳过，只有插件流程才置 `EnableRemoteContentProbe` |
| **追更队列批处理** | `POST /emby/Users/{uid}/FavoriteItems/{itemId}` 然后 `DELETE` 同一个 URL 还原 | 触发 `OnUserDataSaved` → 入队。`UnlockIntroSkip=false` 时 Episode 会进 `MediaInfoExtractItemQueue` |
| 截图 | 先删海报，再 `imageRefreshMode=FullRefresh&replaceAllImages=true`，并临时开 `EnableImageCapture` | 改 JSON + 重启 |
| **片头片尾 / 播放会话** | `POST /emby/Sessions/Playing`、`/Playing/Progress`、`/Playing/Stopped` | 见下方配方，无需真的播放 |
| **设置页保存 / 选项即时生效** | `POST /emby/UI/Command`，`CommandId=PageSave` | `Data` 必须是 JSON 字符串，见下方配方 |
| 查看任务列表 | `GET /emby/ScheduledTasks` | 用 `Category` 过滤（插件任务是「神医助手 Plus」） |

### 配方：模拟一次播放会话

不用真的播放，直接打 Emby 的会话上报接口：

```bash
PSID="ps-test-$(date +%s)"
BODY='{"ItemId":"901","PlaySessionId":"'$PSID'","PositionTicks":0,
       "CanSeek":true,"IsPaused":false,"IsMuted":false,"PlayMethod":"DirectStream"}'
for s in Playing Playing/Progress Playing/Stopped; do
  curl -s --noproxy '*' -o /dev/null -w "$s -> %{http_code}\n" \
    -X POST "http://127.0.0.1:8096/emby/Sessions/$s?api_key=$TOKEN" \
    -H "Content-Type: application/json" --data-binary "$BODY"
done
```

- `PositionTicks` 保持 0，避免触发 `UpdateCreditsTask` 真的写章节标记。
- 想验证「条目缺 `RunTimeTicks`」的分支，挑一个 `MediaItems.RunTimeTicks is null` 的条目。
- 期望：三个都是 204，插件日志无 Warn/Error，`Chapters3` 计数不变。

### 配方：改设置项并验证即时生效

```python
# 1. GET 页面，取 EditObjectContainer.Object
# 2. 只改目标字段
obj['AboutOptions']['DefaultUICulture'] = 'en-US'
# 3. Data 必须是 JSON 字符串（前端 stringify 了两次）
payload = {"PageId": "63c322:Settings", "CommandId": "PageSave",
           "Data": json.dumps(obj, ensure_ascii=False), "ClientLocale": "zh-cn"}
```

```bash
curl -s -X POST "http://127.0.0.1:8096/emby/UI/Command" \
  -H "X-Emby-Token: $TOKEN" -H "Content-Type: application/json" \
  --data-binary @payload.json
```

> 🔴 改之前**务必备份** `StrmAssistant.json`。`Data` 传成嵌套对象会返回 200 但把整页落成默认值。
> 回读 JSON 时顺手核对其它组的取值有没有被清掉。

**页面 id 对照**

| 页面 | PageId |
|---|---|
| 基础设置 | `63c322:Settings` ← **不是 `HomePageView`** |
| 高级功能 | `63c322:ExperienceEnhancePageView` |
| 媒体信息提取 | `63c322:MediaInfoExtractPageView` |
| 元数据增强 | `63c322:MetadataEnhancePageView` |
| 片头片尾 | `63c322:IntroSkipPageView` |
| 网络代理 | `63c322:NetworkProxyPageView` |

## 3. 查日志

```bash
cd "D:/下载/embyserver-win-x64-4.10.1.0/programdata/logs"
grep -a "StrmAssistantPlus" embyserver.txt | tail -30
```

常用关键字：

| 关键字 | 含义 |
|---|---|
| `All N patches initialized successfully` | N 应为 37 |
| `Startup Health Check: Healthy` | 健康检查 |
| `MediaInfoExtract - Item processed:` | 条目提取成功 |
| `MediaInfoExtract - Item skipped or non-existent` | 跳过了（常见于 strm 读不出来） |
| `MediaInfoExtract - FfProbe Error` | 探测失败（远端端点不可达等） |
| `MediaInfoExtract - Clear Item Queue Stopped` | 一批追更处理完毕 |
| `MemoryCleaner [SetProcessWorkingSetSize]` | 内存归还（批末会紧跟一条） |
| `playlist chapters resolved for ... : N` | ISO 章节解析出 N 条 |
| `flattened ... into top-level groups` | 配置迁移 |
| `split UIFunctionOptions into 3 groups` | 配置迁移 |

> ⚠️ **`Logger.Debug` 在 `embyserver.txt` 里看不到**。诊断期的埋点用 `Info`，验证完再降级。

## 4. 查数据库

```python
import sqlite3
c = sqlite3.connect('programdata/data/library.db')

# 章节：表名是 Chapters3（4.10），不是 Chapters2
c.execute('select count(*) from Chapters3 where ItemId=?', (item_id,))

# 媒体流
c.execute('select count(*) from MediaStreams2 where ItemId=?', (item_id,))

# 找条目（表名是 MediaItems，不是 TypedBaseItems）
c.execute("select Id, Name, Path from MediaItems where Path like '%.strm' limit 5")
```

**item id 会随文件移动而变**，别记死数字，先查再跑。

## 5. 配置在哪

`programdata/plugins/configurations/` 下按 Store 拆成多个文件：

```
StrmAssistant.json                         主选项（TaskConcurrency / Memory / ChineseSearch / RuntimeStatus / AboutOptions）
StrmAssistant_MediaInfoExtractOptions.json 媒体信息提取 + ISO
StrmAssistant_MetadataEnhanceOptions.json  元数据增强 + 缓存
StrmAssistant_ExperienceEnhanceOptions.json 高级功能（含拆分后的 3 组）
StrmAssistant_IntroSkipOptions.json        片头片尾
StrmAssistant_NetworkProxyOptions.json     网络代理
```

**改完必须重启**（没有标准配置接口）。

## 6. 环境还原清单

验证完别忘了：

- [ ] 临时改过的选项改回去（`CatchupMinWaitSeconds`、`ClearRecentTmdbCacheOnStartup`、`EnableImageCapture` 等）
- [ ] 删掉测试时上传/替换的海报
- [ ] 卸载测试挂载的盘（`IsoMounter.DismountAll`，或手动 `Dismount-DiskImage`）
- [ ] 还原收藏/已看状态
- [ ] 删掉 `_emby_tmp` 里的临时产物（**不要放在仓库根**，未被 `.gitignore`）

## 7. 已知环境坑

1. **`D:\视频\小号\最近接收\` 下 `.strm` 对本机进程读写被安全策略拦截**
   （`UnauthorizedAccessException`，连 `cd` 都不行）。但 **Emby 自己读得动**。
   所以要构造入库事件做验证，这条路走不通 —— 让用户操作，或者换别的手段。
2. **对不存在的 itemId 调 `POST /Items/{id}/Refresh` 返回 500 + Emby 自身 NRE**
   （`Emby.Api.ItemRefreshService.Post`），**不是插件 bug**。先确认 item 存在。
3. **`ClientLocale` 对插件页面基本无效**：`AboutOptions.DefaultUICulture` 目前硬编码 `zh-CN`，
   传 `en-us` 也返回中文。
