using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace StrmAssistant.Web.Api
{
    [Route("/MultiVersion/Info", "GET", IsHidden = true)]
    [Unauthenticated]
    public class GetMultiVersionInfo
    {
        [ApiMember(Name = "Id", Description = "Item id for full media source list", IsRequired = false,
            DataType = "string", ParameterType = "query")]
        public string Id { get; set; }

        [ApiMember(Name = "Ids", Description = "Comma separated item ids for first-source info", IsRequired = false,
            DataType = "string", ParameterType = "query")]
        public string Ids { get; set; }
    }
}
