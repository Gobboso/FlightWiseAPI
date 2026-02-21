using FlightWiseAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlightWiseAPI.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        private readonly FlightsService _flights;

        public TestController(FlightsService flights)
        {
            _flights = flights;
        }

        [HttpGet("flights")]
        public async Task<IActionResult> TestFlights(
            [FromQuery] string origin = "BOG",
            [FromQuery] string destination = "MDE",
            [FromQuery] string date = "2024-02-25")
        {
            var result = await _flights.BuscarVuelos(origin, destination, date);
            return Content(result, "application/json");
        }

        [HttpGet("airport-code")]
        public IActionResult TestAirportCode([FromQuery] string city)
        {
            var code = Utils.AirportMapper.GetCode(city);
            return Ok(new { city, code });
        }
    }
}
