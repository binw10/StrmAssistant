using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Services;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using static StrmAssistant.Options.MultipleVersionsOptions;

namespace StrmAssistant.Web.Service
{
    public class MultiVersionService : IService
    {
        public class MultiVersionSource
        {
            public string Container { get; set; }
            public long? Size { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
            public string Label { get; set; }
            public string Hdr { get; set; }
            public long? Bitrate { get; set; }
            public string Path { get; set; }
        }

        public class MultiVersionItemInfo : MultiVersionSource
        {
            public string ItemId { get; set; }
        }

        public class MultiVersionResponse
        {
            public bool Enabled { get; set; }
            public List<MultiVersionSource> Sources { get; set; } = new List<MultiVersionSource>();
            public List<MultiVersionItemInfo> Items { get; set; } = new List<MultiVersionItemInfo>();
        }

        private readonly ILibraryManager _libraryManager;

        public MultiVersionService(ILibraryManager libraryManager)
        {
            _libraryManager = libraryManager;
        }

        public object Get(GetMultiVersionInfo request)
        {
            var response = new MultiVersionResponse();

            if (!Plugin.Instance.ExperienceEnhanceStore.GetOptions().EnableMultiVersionEnhance)
            {
                return response;
            }

            response.Enabled = true;

            if (!string.IsNullOrWhiteSpace(request?.Id) && long.TryParse(request.Id.Trim(), out var itemId))
            {
                var item = _libraryManager.GetItemById(itemId);

                if (item != null)
                {
                    response.Sources = GetSources(item);
                }

                return response;
            }

            if (!string.IsNullOrWhiteSpace(request?.Ids))
            {
                var itemIds = request.Ids
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim())
                    .Where(i => long.TryParse(i, out _))
                    .Take(80)
                    .ToList();

                foreach (var id in itemIds)
                {
                    var item = _libraryManager.GetItemById(long.Parse(id));

                    if (item == null) continue;

                    var sources = GetSources(item);
                    var primary = sources.FirstOrDefault();

                    if (primary == null) continue;

                    response.Items.Add(new MultiVersionItemInfo
                    {
                        ItemId = id,
                        Container = primary.Container,
                        Size = primary.Size,
                        Width = primary.Width,
                        Height = primary.Height,
                        Label = primary.Label,
                        Hdr = primary.Hdr,
                        Bitrate = primary.Bitrate
                    });
                }
            }

            return response;
        }

        private List<MultiVersionSource> GetSources(BaseItem item)
        {
            var libraryOptions = Plugin.LibraryManager.GetLibraryOptions(item);
            var uiOptions = Plugin.Instance.ExperienceEnhanceStore.GetOptions();

            var sources = item.GetMediaSources(false, false, libraryOptions)
                .Select(source =>
                {
                    var video = source.MediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);
                    var path = source.Path;

                    var label = uiOptions.MultiVersionDisplayOption switch
                    {
                        MultiVersionDisplayOption.FileNameWithoutExtension => string.IsNullOrEmpty(path)
                            ? null
                            : System.IO.Path.GetFileNameWithoutExtension(path),
                        MultiVersionDisplayOption.SystemDefault => null,
                        _ => GetQualityLabel(video?.Height) // MediaInfo（默认）
                    };

                    return new MultiVersionSource
                    {
                        Container = source.Container,
                        Size = source.Size,
                        Width = video?.Width,
                        Height = video?.Height,
                        Label = label,
                        Hdr = GetHdrLabel(source.MediaStreams),
                        Bitrate = source.Bitrate,
                        Path = path
                    };
                })
                .ToList();

            sources = uiOptions.MultiVersionSortOption switch
            {
                MultiVersionSortOption.HighToLowQuality => sources
                    .OrderByDescending(s => QualityScore(s.Height, s.Hdr))
                    .ToList(),
                MultiVersionSortOption.LowToHighQuality => sources
                    .OrderBy(s => QualityScore(s.Height, s.Hdr))
                    .ToList(),
                MultiVersionSortOption.PathAscending => sources.OrderBy(s => s.Path, StringComparer.OrdinalIgnoreCase).ToList(),
                MultiVersionSortOption.PathDescending => sources.OrderByDescending(s => s.Path, StringComparer.OrdinalIgnoreCase).ToList(),
                _ => sources // SystemDefault：保持 Emby 原始顺序
            };

            return sources;
        }

        /// <summary>
        /// 画质评分：分辨率为主，HDR/DV 加成（用于排序比较）。
        /// 档位顺序须与 <c>strmassistant.js</c> 的 <c>hdrs</c> 列表保持一致（前 = 最优）。
        /// DV 细分档：原盘双层 P7 &gt; 单层 P8 &gt; P9 &gt; 流媒体 P5 &gt; 未识别 DV。
        /// </summary>
        private static int QualityScore(int? height, string hdr)
        {
            var score = height ?? 0;
            if (string.IsNullOrEmpty(hdr)) return score;

            if (hdr == "DV P7") score += 520;
            else if (hdr == "DV P8") score += 515;
            else if (hdr == "DV P9") score += 510;
            else if (hdr == "DV P5") score += 505;
            else if (hdr == "DV") score += 500;
            else if (hdr == "HDR10+") score += 400;
            else if (hdr == "HDR Vivid") score += 350;
            else if (hdr == "HDR") score += 300;

            return score;
        }

        private static string GetQualityLabel(int? height)
        {
            if (!height.HasValue || height.Value <= 0) return null;

            if (height >= 3800) return "8K";
            if (height >= 2100) return "4K";
            if (height >= 1400) return "1440p";
            if (height >= 1000) return "1080p";
            if (height >= 700) return "720p";

            return "SD";
        }

        /// <summary>
        /// HDR 标签。优先按 DV Profile 细分档：
        /// 双层 DV Profile 7 原盘的**增强层**会被 <c>DolbyVisionProfile</c> 标注为
        /// <c>ExtendedVideoSubType = DoviProfile76</c>，而基础层仍是 HDR10，
        /// 因此这里需要扫描**全部视频流**取最优档位，只看主视频流会漏掉 DV 信息。
        /// 档位顺序须与 <c>strmassistant.js</c> 的 <c>hdrs</c> 列表保持一致（前 = 最优）。
        /// </summary>
        private static string GetHdrLabel(IEnumerable<MediaStream> streams)
        {
            var videos = streams?
                .Where(s => s != null && s.Type == MediaStreamType.Video)
                .ToList();

            if (videos == null || videos.Count == 0) return null;

            foreach (var video in videos)
            {
                var dvLabel = GetDvProfileLabel(video.ExtendedVideoSubType);
                if (dvLabel != null) return dvLabel;
            }

            var videoRange = videos[0].VideoRange;
            if (string.IsNullOrEmpty(videoRange)) return null;

            if (videoRange.IndexOf("DoVi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                videoRange.IndexOf("Dovi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "DV";
            }

            if (videoRange.IndexOf("HDR10Plus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                videoRange.IndexOf("HDR10+", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HDR10+";
            }

            if (videoRange.IndexOf("Vivid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HDR Vivid";
            }

            if (videoRange.IndexOf("HDR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HDR";
            }

            return null;
        }

        /// <summary>
        /// Emby 的 <c>ExtendedVideoSubTypes</c> 命名规则为「profile × 10 + level」，
        /// 例如 <c>DoviProfile76</c> = Profile 7。此处按 Profile 归并出对外标签。
        /// 其余 Dolby Vision 取值（如 Profile 4/6）返回 null，回落为笼统的 "DV"。
        /// </summary>
        private static string GetDvProfileLabel(ExtendedVideoSubTypes subType)
        {
            switch (subType)
            {
                case ExtendedVideoSubTypes.DoviProfile76:
                    return "DV P7";
                case ExtendedVideoSubTypes.DoviProfile81:
                case ExtendedVideoSubTypes.DoviProfile82:
                case ExtendedVideoSubTypes.DoviProfile83:
                case ExtendedVideoSubTypes.DoviProfile84:
                case ExtendedVideoSubTypes.DoviProfile85:
                    return "DV P8";
                case ExtendedVideoSubTypes.DoviProfile92:
                    return "DV P9";
                case ExtendedVideoSubTypes.DoviProfile50:
                    return "DV P5";
                default:
                    return null;
            }
        }
    }
}
