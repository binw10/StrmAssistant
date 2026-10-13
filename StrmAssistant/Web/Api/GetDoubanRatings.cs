using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/Douban/Ratings", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetDoubanRatings
    {
        [ApiMember(Name = "Ids", Description = "Comma separated item ids", IsRequired = false,
            DataType = "string", ParameterType = "query")]
        public string Ids { get; set; }
    }
}
