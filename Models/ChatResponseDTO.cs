namespace FlightWiseAPI.Models
{
    public class ChatResponseDto
    {
        public string SessionId { get; set; }
        public string Response { get; set; }
        public string Intent { get; set; }
        public bool IsFlightSearch { get; set; }
    }
}
