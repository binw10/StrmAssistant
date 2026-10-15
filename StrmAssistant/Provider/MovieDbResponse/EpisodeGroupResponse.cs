using System;
using System.Collections.Generic;

namespace StrmAssistant.Provider
{
    public class EpisodeGroupResponse
    {
        public string description { get; set; }
        public List<EpisodeGroup> groups { get; set; }
        public string id { get; set; }
    }

    public class EpisodeGroup
    {
        public string name { get; set; }
        public int order { get; set; }
        public List<GroupEpisode> episodes { get; set; }

        // ---- 自定义扩展字段（EnableMovieDbEpisodeGroupExtendedInfo 开启时生效）----
        // 自定义季简介；留空则季简介维持置空
        public string overview { get; set; }
        // 自定义季首播日期（ISO 8601）；留空则首播维持置空
        public DateTimeOffset? air_date { get; set; }
    }

    public class GroupEpisode
    {
        public DateTimeOffset air_date { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public string overview { get; set; }
        public int episode_number { get; set; }
        public int season_number { get; set; }
        public int order { get; set; }
    }
    
    public class CompactEpisodeGroupResponse
    {
        public string description { get; set; }
        public List<CompactEpisodeGroup> groups { get; set; }
        public string id { get; set; }
    }

    public class CompactEpisodeGroup
    {
        public string name { get; set; }
        public int order { get; set; }
        public List<CompactGroupEpisode> episodes { get; set; }
    }

    public class CompactGroupEpisode
    {
        public int episode_number { get; set; }
        public int season_number { get; set; }
        public int order { get; set; }
    }
}
