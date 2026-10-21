using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/StrmAssistant/Items/DeleteMediaInfo", "POST")]
    [Authenticated(Roles = "Admin")]
    public class DeleteMediaInfo : IReturn<MediaInfoMenuResponse>, IReturn
    {
        public string[] Ids { get; set; }
    }
}
