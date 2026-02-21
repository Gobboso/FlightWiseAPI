using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace FlightWiseAPI.Services
{
    public class FlightsService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<FlightsService> _logger;

        public FlightsService(IConfiguration config, HttpClient http, ILogger<FlightsService> logger)
        {
            _http = http;
            _apiKey = config["API_Keys:SerpAPI:SERPAPI_API_KEY"];
            _logger = logger;
        }

        public async Task<string> BuscarVuelos(
            string origen,
            string destino,
            string fechaSalida = null,
            string fechaRegreso = null,
            int adultos = 1)
        {
            try
            {
                // Buscar en USD y COP
                var flightsUSD = await BuscarVuelosEnMoneda(origen, destino, fechaSalida, fechaRegreso, adultos, "USD");
                var flightsCOP = await BuscarVuelosEnMoneda(origen, destino, fechaSalida, fechaRegreso, adultos, "COP");

                // Combinar resultados
                var combined = new
                {
                    flights_usd = flightsUSD,
                    flights_cop = flightsCOP,
                    search_info = new
                    {
                        origin = origen,
                        destination = destino,
                        outbound_date = fechaSalida,
                        return_date = fechaRegreso,
                        adults = adultos
                    }
                };

                return JsonSerializer.Serialize(combined);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error general al buscar vuelos: {ex.Message}");
                return JsonSerializer.Serialize(new
                {
                    error = true,
                    user_message = "No pudimos encontrar vuelos en este momento. Por favor intenta nuevamente o prueba con otras fechas."
                });
            }
        }

        private async Task<object> BuscarVuelosEnMoneda(
            string origen,
            string destino,
            string fechaSalida,
            string fechaRegreso,
            int adultos,
            string moneda)
        {
            try
            {
                var query = new List<string>
                {
                    "engine=google_flights",
                    $"departure_id={origen}",
                    $"arrival_id={destino}",
                    $"adults={adultos}",
                    $"currency={moneda}",
                    "hl=es",
                    $"api_key={_apiKey}"
                };
                
                bool isRoundTrip = !string.IsNullOrEmpty(fechaRegreso);
                
                if (!string.IsNullOrEmpty(fechaSalida))
                    query.Add($"outbound_date={fechaSalida}");

                if (isRoundTrip)
                {
                    query.Add($"return_date={fechaRegreso}");
                    query.Add("type=1");
                }
                else
                {
                    query.Add("type=2");
                }

                var url = "https://serpapi.com/search.json?" + string.Join("&", query);

                _logger.LogInformation($"Buscando vuelos ({moneda}): {origen} -> {destino}, Fecha: {fechaSalida}, Tipo: {(isRoundTrip ? "Ida y vuelta" : "Solo ida")}");

                var res = await _http.GetAsync(url);
                var resultJson = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Error de SerpAPI ({moneda}): Status {res.StatusCode}");
                    return new { error = true, currency = moneda };
                }

                var flightDoc = JsonDocument.Parse(resultJson);
                return new
                {
                    currency = moneda,
                    data = flightDoc.RootElement.Clone()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error buscando vuelos en {moneda}: {ex.Message}");
                return new { error = true, currency = moneda };
            }
        }
    }
}
