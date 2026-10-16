using MediaBrowser.Model.Services;
using StrmAssistant.Provider.Douban;
using StrmAssistant.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Web.Service
{
    public class DoubanService : IService
    {
        public class DoubanRatingsResponse
        {
            public bool Enabled { get; set; }
            public List<DoubanAssist.DoubanRatingInfo> Ratings { get; set; } = new List<DoubanAssist.DoubanRatingInfo>();
        }

        public object Get(GetDoubanRatings request)
        {
            var response = new DoubanRatingsResponse();

            if (!Plugin.Instance.MetadataEnhanceStore.GetOptions().DoubanAssist.EnableDoubanAssist)
            {
                return response;
            }

            response.Enabled = true;

            if (string.IsNullOrWhiteSpace(request?.Ids))
            {
                return response;
            }

            var itemIds = request.Ids
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .Where(i => long.TryParse(i, out _))
                .Take(80)
                .ToList();

            response.Ratings = Plugin.DoubanAssist.GetRatings(itemIds);

            return response;
        }
    }
}
