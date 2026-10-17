using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/{Web}/modules/mediainfo/mediainfo.js", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetMediaInfoJs
    {
        public string Web { get; set; }
    }
}
