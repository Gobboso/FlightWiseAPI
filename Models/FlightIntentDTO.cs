namespace FlightWiseAPI.Models
{
    public class FlightIntentDTO
    {
        public string? intent { get; set; }
        public string? origin { get; set; }
        public string? destination { get; set; }
        public string? date { get; set; }
        public string? returnDate { get; set; }
        public int adults { get; set; }
        public List<string>? missing { get; set; }
        public string? city { get; set; }
        public string? response { get; set; }
    }
}
