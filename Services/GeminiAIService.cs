using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FlightWiseAPI.Services
{
    public class GeminiAIService
    {
        private readonly string _apiKey;
        private readonly HttpClient _http;
        private readonly ILogger<GeminiAIService> _logger;
        private const int MaxRetries = 3;
        private const int TimeoutSeconds = 30;

        public GeminiAIService(IConfiguration config, HttpClient httpClient, ILogger<GeminiAIService> logger)
        {
            _apiKey = config["API_Keys:Gemini:GEMINI_API_KEY"];
            _http = httpClient;
            _logger = logger;
            _http.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        }

        // thinkingBudget: 0 = desactiva el razonamiento → todos los tokens van al texto real
        // thinkingBudget: N = reserva N tokens para thinking (solo donde aporta valor)
        public async Task<string> AskGemini(string prompt, int maxOutputTokens = 800, double temperature = 0.7, int thinkingBudget = 0)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent?key={_apiKey}";

            var body = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    maxOutputTokens,
                    temperature,
                    thinkingConfig = new { thinkingBudget }
                }
            };

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var json = JsonSerializer.Serialize(body);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    _logger.LogInformation("AskGemini - Intento {Attempt}/{MaxRetries}", attempt, MaxRetries);
                    var response = await _http.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("AskGemini - Error HTTP {StatusCode}: {Error}", response.StatusCode, errorContent);

                        if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                        {
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(2000 * attempt);
                                continue;
                            }
                        }

                        throw new HttpRequestException($"HTTP {response.StatusCode}: {errorContent}");
                    }

                    var resultJson = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrWhiteSpace(resultJson))
                        throw new InvalidOperationException("Respuesta vacía de Gemini");

                    using var doc = JsonDocument.Parse(resultJson);

                    if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                        candidates.GetArrayLength() == 0)
                    {
                        _logger.LogWarning("AskGemini - Estructura inesperada: {Json}", resultJson);
                        throw new InvalidOperationException("Respuesta de Gemini sin contenido válido");
                    }

                    var text = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    if (string.IsNullOrWhiteSpace(text))
                        throw new InvalidOperationException("Respuesta de Gemini vacía");

                    _logger.LogInformation("AskGemini - Éxito");
                    return text;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning("AskGemini - Timeout en intento {Attempt}/{MaxRetries}: {Message}", attempt, MaxRetries, ex.Message);
                    if (attempt == MaxRetries) throw;
                    await Task.Delay(1000 * attempt);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning("AskGemini - Error de conexión en intento {Attempt}/{MaxRetries}: {Message}", attempt, MaxRetries, ex.Message);
                    if (attempt == MaxRetries) throw;
                    await Task.Delay(1000 * attempt);
                }
                catch (JsonException ex)
                {
                    _logger.LogError("AskGemini - Error al parsear JSON: {Message}", ex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError("AskGemini - Error inesperado {Attempt}/{MaxRetries}: {Type} - {Message}", attempt, MaxRetries, ex.GetType().Name, ex.Message);
                    if (attempt == MaxRetries) throw;
                    await Task.Delay(1000 * attempt);
                }
            }

            throw new InvalidOperationException("AskGemini falló después de todos los reintentos");
        }

        // thinkingBudget: 0 — lookup trivial, no necesita razonar
        public async Task<string> ResolveAirportCode(string cityName)
        {
            try
            {
                var prompt = $"Código IATA de {cityName}. Solo las 3 letras en mayúsculas, nada más.";
                var response = await AskGemini(prompt, maxOutputTokens: 20, temperature: 0.1, thinkingBudget: 0);
                return response.Trim().ToUpper();
            }
            catch (Exception ex)
            {
                _logger.LogError("ResolveAirportCode - Error resolviendo {City}: {Message}", cityName, ex.Message);
                throw;
            }
        }

        // thinkingBudget: 0 — sigue una plantilla JSON fija, no necesita razonar
        public async Task<string> DetectIntent(string userMessage, string chatHistory)
        {
            try
            {
                var todayFormatted = DateTime.Now.ToString("yyyy-MM-dd");

                var prompt = $@"Eres FlightWise, asistente de viajes amable. Hoy: {todayFormatted}.

Historial:
{chatHistory}

Mensaje: {userMessage}

Responde SOLO JSON sin markdown, eligiendo uno de estos formatos:

Vuelos con todos los datos:
{{""intent"":""ask_flights"",""origin"":""ciudad"",""destination"":""ciudad"",""date"":""YYYY-MM-DD"",""returnDate"":"""",""adults"":1,""missing"":[]}}

Vuelos con datos incompletos (en response escribe UNA pregunta amable pidiendo lo que falta):
{{""intent"":""ask_flights"",""origin"":"""",""destination"":"""",""date"":"""",""returnDate"":"""",""adults"":1,""missing"":[""origin"",""date""],""response"":""Pregunta aquí""}}

Actividades en ciudad conocida:
{{""intent"":""ask_activities"",""city"":""ciudad"",""response"":""""}}

Actividades sin ciudad (en response pregunta cuál ciudad):
{{""intent"":""ask_activities"",""city"":"""",""response"":""Pregunta aquí""}}

Cualquier otro mensaje (en response escribe la respuesta directa, máximo 2 líneas, tono cálido):
{{""intent"":""chat"",""response"":""Respuesta aquí""}}

JSON:";

                var response = await AskGemini(prompt, maxOutputTokens: 500, temperature: 0.4, thinkingBudget: 0);

                response = response.Trim();
                if (response.StartsWith("```"))
                    response = response.Replace("```json", "").Replace("```", "").Trim();

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError("DetectIntent - Error: {Message}", ex.Message);
                throw;
            }
        }

        // thinkingBudget: 0 — formatea datos existentes, no necesita razonar
        public async Task<string> GenerateFlightResponse(string flightData)
        {
            try
            {
                var prompt = $@"Extrae los 3 vuelos más económicos de estos datos JSON y preséntalos EXACTAMENTE en este formato, con una línea en blanco entre cada vuelo, sin emojis, sin texto adicional:

Aerolínea: $XX USD / $XX.XXX COP | HH:MM | Xh Xm | Directo

Aerolínea: $XX USD / $XX.XXX COP | HH:MM | Xh Xm | 1 escala

Reglas:
- Exactamente 3 entradas separadas por línea en blanco (o menos si no hay suficientes vuelos)
- Sin encabezados, sin texto antes ni después
- Precio COP con puntos como separador de miles
- Si no hay vuelos: ""No hay vuelos disponibles. ¿Te gustaría probar otra fecha o destino?""

Datos: {flightData}";

                return await AskGemini(prompt, maxOutputTokens: 300, temperature: 0.1, thinkingBudget: 0);
            }
            catch (Exception ex)
            {
                _logger.LogError("GenerateFlightResponse - Error: {Message}", ex.Message);
                throw;
            }
        }

        // thinkingBudget: 512 — contenido creativo, el razonamiento mejora la calidad
        public async Task<string> GenerateActivitiesResponse(string city)
        {
            try
            {
                var prompt = $@"Eres FlightWise, guía de viajes. Qué hacer en {city}.

Responde EXACTEMENTE en este formato, cada ítem en su propia línea (usa saltos de linea entre cada lugar), sin párrafos introductorios:

🏛️ Lugar1: Una línea de descripción.
🌿 Lugar2: Una línea de descripción.
🎭 Lugar3: Una línea de descripción.
🎨 Lugar4: Una línea de descripción.

🍽️ Plato1: Una línea de descripción.
🥘 Plato2: Una línea de descripción.

💡 Consejo práctico en una línea o dos.

Reglas:
- Sin introducción ni despedida
- Cada ítem en su propia línea
- Línea en blanco entre cada sección (actividades, gastronomía, consejo)
- Al momento de usar nombres, ponlos en negrita
- Tono cálido y muy motivador";

                return await AskGemini(prompt, maxOutputTokens: 1200, temperature: 0.8, thinkingBudget: 512);
            }
            catch (Exception ex)
            {
                _logger.LogError("GenerateActivitiesResponse - Error para {City}: {Message}", city, ex.Message);
                throw;
            }
        }
    }
}
