using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/StrmAssistant/Items/ExtractMediaInfo", "POST")]
    [Authenticated(Roles = "Admin")]
    public class ExtractMediaInfo : IReturn<MediaInfoMenuResponse>, IReturn
    {
        public string[] Ids { get; set; }
    }
}
