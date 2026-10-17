using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace StrmAssistant.Provider
{
    /// <summary>
    /// 豆瓣外部链接（D1）：为 ProviderIds["Douban"] 生成豆瓣条目链接，
    /// 使元数据管理器/详情页能展示豆瓣图标并跳转到豆瓣条目（社区版此前缺失）。
    /// Key 必须与 DoubanAssist 写入的 ProviderId 键名一致（"Douban"）。
    /// </summary>
    public class DoubanExternalId : IExternalId, IHasWebsite
    {
        public string Name => StaticName;

        public string Key => StaticName;

        public string UrlFormatString => "https://movie.douban.com/subject/{0}/";

        public string Website => "https://movie.douban.com";

        public bool Supports(IHasProviderIds item) => item is Movie || item is Series || item is Season;

        public static string StaticName => "Douban";
    }
}
