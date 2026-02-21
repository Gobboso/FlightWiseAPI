using FlightWiseAPI.Memory;
using FlightWiseAPI.Models;
using FlightWiseAPI.Services;
using FlightWiseAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FlightWiseAPI.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly GeminiAIService _gemini;
        private readonly FlightsService _flights;
        private readonly ILogger<ChatController> _logger;

        public ChatController(GeminiAIService gemini, FlightsService flights, ILogger<ChatController> logger)
        {
            _gemini = gemini;
            _flights = flights;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ChatResponseDto), 200)]
        public async Task<IActionResult> Chat([FromBody] ChatDto dto)
        {
            _logger.LogInformation("🔵 POST /api/chat - Petición recibida");
            _logger.LogInformation("🔵 Origin: {Origin}", Request.Headers["Origin"].ToString());
            _logger.LogInformation("🔵 Content-Type: {ContentType}", Request.ContentType);

            try
            {
                var sessionId = string.IsNullOrWhiteSpace(dto.SessionId)
                    ? Guid.NewGuid().ToString()
                    : dto.SessionId.Trim();

                _logger.LogInformation("Chat - SessionId: {SessionId}, Message: {Message}", sessionId, dto.Message);

                var history = ChatMemory.GetFormattedHistory(sessionId);
                ChatMemory.AddMessage(sessionId, "user", dto.Message);

                // 1 llamada: clasifica el intent Y genera la respuesta para chat/missing
                var intentJson = await _gemini.DetectIntent(dto.Message, history);

                FlightIntentDTO intent;
                try
                {
                    intent = JsonSerializer.Deserialize<FlightIntentDTO>(intentJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new FlightIntentDTO { intent = "chat", response = "¡Hola! Soy FlightWise. ¿En qué puedo ayudarte? 😊" };
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Error al parsear intent: {Message}", ex.Message);
                    intent = new FlightIntentDTO { intent = "chat", response = "¡Hola! Soy FlightWise. ¿En qué puedo ayudarte? 😊" };
                }

                string aiResponse;
                bool isFlightSearch = false;

                if (intent.intent == "ask_flights")
                {
                    isFlightSearch = true;

                    // Datos incompletos → respuesta ya viene embebida en el intent, sin llamada extra
                    if (intent.missing != null && intent.missing.Count > 0)
                    {
                        aiResponse = !string.IsNullOrWhiteSpace(intent.response)
                            ? intent.response
                            : "Para buscar el vuelo necesito origen, destino y fecha. ¿Me los indicas? 😊";

                        ChatMemory.AddMessage(sessionId, "assistant", aiResponse);
                        return Ok(new ChatResponseDto
                        {
                            SessionId = sessionId,
                            Response = aiResponse,
                            Intent = "ask_flights",
                            IsFlightSearch = false
                        });
                    }

                    // Resolver códigos IATA en paralelo si es necesario
                    var originCode = AirportMapper.GetCode(intent.origin ?? string.Empty);
                    var destCode = AirportMapper.GetCode(intent.destination ?? string.Empty);

                    var originNeedsResolve = originCode.Length != 3 || originCode == originCode.ToLower();
                    var destNeedsResolve = destCode.Length != 3 || destCode == destCode.ToLower();

                    if (originNeedsResolve || destNeedsResolve)
                    {
                        var originTask = originNeedsResolve
                            ? _gemini.ResolveAirportCode(intent.origin ?? string.Empty)
                            : Task.FromResult(originCode);

                        var destTask = destNeedsResolve
                            ? _gemini.ResolveAirportCode(intent.destination ?? string.Empty)
                            : Task.FromResult(destCode);

                        var codes = await Task.WhenAll(originTask, destTask);
                        originCode = codes[0];
                        destCode = codes[1];
                    }

                    _logger.LogInformation("Códigos IATA: {Origin} → {OriginCode}, {Dest} → {DestCode}",
                        intent.origin, originCode, intent.destination, destCode);

                    var flightsData = await _flights.BuscarVuelos(
                        originCode,
                        destCode,
                        intent.date,
                        intent.returnDate,
                        intent.adults == 0 ? 1 : intent.adults
                    );

                    // Verificar error en SerpAPI
                    try
                    {
                        var flightDoc = JsonDocument.Parse(flightsData);
                        if (flightDoc.RootElement.TryGetProperty("error", out var errorProp) && errorProp.GetBoolean())
                        {
                            aiResponse = "No encontré vuelos disponibles para esas fechas y ciudades. ¿Te gustaría intentar con otras fechas?";
                            ChatMemory.AddMessage(sessionId, "assistant", aiResponse);
                            return Ok(new ChatResponseDto
                            {
                                SessionId = sessionId,
                                Response = aiResponse,
                                Intent = "ask_flights",
                                IsFlightSearch = true
                            });
                        }
                    }
                    catch (JsonException) { }

                    // Firma actualizada: solo recibe flightData
                    aiResponse = await _gemini.GenerateFlightResponse(flightsData);

                    // Agregar link de Google Flights
                    try
                    {
                        var flightDoc = JsonDocument.Parse(flightsData);
                        if (flightDoc.RootElement.TryGetProperty("flights_usd", out var flightsUsd) &&
                            flightsUsd.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("search_metadata", out var metadata) &&
                            metadata.TryGetProperty("google_flights_url", out var urlProp))
                        {
                            var googleFlightsUrl = urlProp.GetString();
                            if (!string.IsNullOrEmpty(googleFlightsUrl))
                                aiResponse += $"\n\n🔗 **[Ver más opciones en Google Flights]({googleFlightsUrl})**";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo extraer Google Flights URL: {Message}", ex.Message);
                    }
                }
                else if (intent.intent == "ask_activities")
                {
                    if (!string.IsNullOrWhiteSpace(intent.city))
                    {
                        // Ciudad conocida → 1 llamada extra para actividades detalladas
                        // Firma actualizada: solo recibe city
                        aiResponse = await _gemini.GenerateActivitiesResponse(intent.city);
                    }
                    else
                    {
                        // Sin ciudad → respuesta ya viene del intent, sin llamada extra
                        aiResponse = !string.IsNullOrWhiteSpace(intent.response)
                            ? intent.response
                            : "¿En qué ciudad te gustaría saber qué hacer? 😊";
                    }
                }
                else
                {
                    // Chat normal → respuesta ya viene del intent, sin llamada extra
                    aiResponse = !string.IsNullOrWhiteSpace(intent.response)
                        ? intent.response
                        : "¡Hola! Soy FlightWise, tu asistente de viajes. ¿En qué puedo ayudarte? 😊";
                }

                ChatMemory.AddMessage(sessionId, "assistant", aiResponse);

                return Ok(new ChatResponseDto
                {
                    SessionId = sessionId,
                    Response = aiResponse,
                    Intent = intent.intent ?? "chat",
                    IsFlightSearch = isFlightSearch
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error en Chat: {Message}", ex.Message);
                var errorSessionId = dto.SessionId ?? Guid.NewGuid().ToString();

                return Ok(new ChatResponseDto
                {
                    SessionId = errorSessionId,
                    Response = "¡Disculpa! Tuve un problema procesando tu solicitud. Por favor intenta nuevamente 😊",
                    Intent = "error",
                    IsFlightSearch = false
                });
            }
        }
    }
}
