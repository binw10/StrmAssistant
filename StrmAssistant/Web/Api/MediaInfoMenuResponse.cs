namespace StrmAssistant.Web.Api
{
    public class MediaInfoMenuResponse
    {
        public int Total { get; set; }
        public int Processed { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int Queued { get; set; }
        public string Message { get; set; }
    }
}
