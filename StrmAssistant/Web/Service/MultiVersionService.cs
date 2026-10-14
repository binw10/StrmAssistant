using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Services;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;

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

        private static List<MultiVersionSource> GetSources(BaseItem item)
        {
            var libraryOptions = Plugin.LibraryManager.GetLibraryOptions(item);

            return item.GetMediaSources(false, false, libraryOptions)
                .Select(source =>
                {
                    var video = source.MediaStreams?.FirstOrDefault(s => s.Type == MediaStreamType.Video);

                    return new MultiVersionSource
                    {
                        Container = source.Container,
                        Size = source.Size,
                        Width = video?.Width,
                        Height = video?.Height,
                        Label = GetQualityLabel(video?.Height),
                        Hdr = GetHdrLabel(video?.VideoRange),
                        Bitrate = source.Bitrate
                    };
                })
                .ToList();
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

        private static string GetHdrLabel(string videoRange)
        {
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

            if (videoRange.IndexOf("HDR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HDR";
            }

            return null;
        }
    }
}
