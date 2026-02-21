# ✈️ FlightWiseAPI

> Backend de un asistente de viajes conversacional impulsado por IA. El usuario escribe en lenguaje natural y el sistema busca vuelos reales, muestra precios en USD y COP, y recomienda actividades en el destino.

![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12-blue?logo=csharp)
![Gemini AI](https://img.shields.io/badge/Gemini-AI-orange?logo=google)
![SerpAPI](https://img.shields.io/badge/SerpAPI-Google%20Flights-green)

---

## 🧠 ¿Qué hace?

El usuario escribe como si hablara con una persona:

```
Usuario:      "Quiero ir de Bogotá a Madrid el 15 de marzo"
FlightWise:   "Avianca: $620 USD / $2.400.000 COP | 10:30 | 10h 20m | 1 escala
               Iberia:  $580 USD / $2.200.000 COP | 14:00 | 11h 00m | Directo
               🔗 Ver más opciones en Google Flights"

Usuario:      "¿Qué puedo hacer en Madrid?"
FlightWise:   "🏛️ Museo del Prado: ..."
```

El sistema detecta la intención, extrae los datos, resuelve los aeropuertos, busca vuelos reales y responde — todo en segundos.

---

## 🛠️ Tecnologías

| Tecnología | Rol |
|---|---|
| ASP.NET Core (.NET 8) + C# 12 | Framework principal |
| Google Gemini AI (`gemini-3-flash-preview`) | Procesamiento de lenguaje natural |
| SerpAPI — Google Flights | Datos de vuelos en tiempo real |
| React (proyecto separado) | Frontend conversacional |
| Swagger / OpenAPI | Documentación interactiva |

---

## 🏗️ Arquitectura

```
FlightWiseAPI/
├── Controllers/
│   ├── ChatController.cs        # POST /api/chat — endpoint principal
│   ├── GeminiAIController.cs    # POST /api/gemini — consulta directa a la IA
│   └── TestController.cs        # GET /api/test/* — endpoints de prueba
├── Services/
│   ├── GeminiAIService.cs       # Integración con Google Gemini AI
│   └── FlightsService.cs        # Integración con SerpAPI (Google Flights)
├── Memory/
│   └── ChatMemory.cs            # Historial de conversación por sesión (en memoria)
├── Models/
│   ├── ChatDTO.cs               # Entrada del chat
│   ├── ChatResponseDTO.cs       # Respuesta del chat
│   └── FlightIntentDTO.cs       # Intención extraída por la IA
├── Utils/
│   └── AirportMapper.cs         # Diccionario estático ciudad → código IATA (+70 ciudades)
├── Program.cs
└── appsettings.example.json     # Plantilla de configuración (sin claves reales)
```

---

## 🔌 Endpoints

### `POST /api/chat`
Endpoint principal del asistente.

**Request:**
```json
{
  "sessionId": "abc-123",
  "message": "Vuelos de Bogotá a Miami para el 15 de abril"
}
```
> `sessionId` es opcional en el primer mensaje. A partir del segundo es obligatorio para mantener el contexto de la conversación.

**Response:**
```json
{
  "sessionId": "abc-123",
  "response": "Avianca: $320 USD / $1.240.000 COP | 09:30 | 3h 15m | Directo\n\n🔗 Ver más opciones en Google Flights",
  "intent": "ask_flights",
  "isFlightSearch": true
}
```

| Campo | Valores posibles |
|---|---|
| `intent` | `ask_flights` · `ask_activities` · `chat` · `error` |
| `isFlightSearch` | `true` si se consultó SerpAPI, `false` en caso contrario |

### `POST /api/gemini`
Envía un prompt libre directamente al modelo de IA.

---

## 🔄 Flujo de una petición

```
POST /api/chat
  │
  ├── 1. Gestión de sessionId (genera uno si no viene)
  ├── 2. Recupera historial de ChatMemory
  │
  ├── 3. DetectIntent() → 1 llamada a Gemini
  │        ├── ask_flights (completo) ──────────────────────────┐
  │        ├── ask_flights (incompleto) → respuesta en el JSON  │
  │        ├── ask_activities (con ciudad) ─────────────────┐   │
  │        ├── ask_activities (sin ciudad) → respuesta JSON │   │
  │        └── chat → respuesta embebida en el JSON         │   │
  │                                                         │   │
  ├── 4a. [ask_activities] GenerateActivitiesResponse() ───┘   │
  │                                                             │
  └── 4b. [ask_flights] ────────────────────────────────────────┘
           ├── AirportMapper.GetCode() → diccionario local
           ├── ResolveAirportCode() → IA como fallback (paralelo)
           ├── BuscarVuelos() → SerpAPI en USD + COP (paralelo)
           ├── GenerateFlightResponse() → formatea la lista
           └── Extrae google_flights_url y lo adjunta
```

---

## ⚙️ Servicios

### `GeminiAIService`

| Método | Descripción |
|---|---|
| `AskGemini(prompt, maxTokens, temperature, thinkingBudget)` | Llamada base a la API con reintentos y backoff exponencial |
| `DetectIntent(message, history)` | Clasifica la intención Y genera la respuesta para chat en **1 sola llamada** |
| `GenerateFlightResponse(flightData)` | Formatea los 3 vuelos más económicos en texto limpio |
| `GenerateActivitiesResponse(city)` | Genera lugares, gastronomía y consejos del destino |
| `ResolveAirportCode(cityName)` | Convierte un nombre de ciudad a código IATA de 3 letras |

### `FlightsService`

| Método | Descripción |
|---|---|
| `BuscarVuelos(origen, destino, fechaSalida, fechaRegreso, adultos)` | Consulta SerpAPI en paralelo en USD y COP, soporta ida y vuelta |

### `ChatMemory`

| Método | Descripción |
|---|---|
| `AddMessage(sessionId, role, message)` | Añade un mensaje al historial de la sesión |
| `GetFormattedHistory(sessionId, maxMessages)` | Devuelve los últimos N mensajes como texto |
| `ClearSession(sessionId)` | Limpia el historial de una sesión |

---

## 💡 Decisiones técnicas

**Fusión de llamadas a la IA**
`DetectIntent` clasifica el mensaje Y genera la respuesta para chat en una sola llamada, reduciendo de 2 a 1. Con el límite de 5 RPM del plan gratuito, esto duplica las conversaciones posibles por minuto.

**`thinkingBudget: 0` en llamadas deterministas**
`gemini-3-flash-preview` es un modelo de razonamiento: los tokens de pensamiento se descuentan del presupuesto de salida. Se desactiva en llamadas mecánicas (clasificar JSON, formatear vuelos) y se reserva para respuestas creativas (actividades turísticas).

**Paralelismo con `Task.WhenAll`**
La resolución de dos códigos IATA y la búsqueda en dos monedas se ejecutan en paralelo. El tiempo de espera es el de la llamada más lenta, no la suma de todas.

**Fallback en cascada para IATA**
Diccionario local (+70 ciudades) → IA. La IA solo se consulta si la ciudad no está en el mapa local.

**Retry con backoff exponencial**
Ante errores 429 o 5xx de Gemini, el sistema reintenta hasta 3 veces con pausas de 2s, 4s y 6s.

---

## 🔒 Configuración

> ⚠️ `appsettings.json` está en `.gitignore` y no se sube al repositorio. Usa `appsettings.example.json` como plantilla.

```json
{
  "API_Keys": {
    "Gemini": {
      "GEMINI_API_KEY": "TU_CLAVE_AQUI"
    },
    "SerpAPI": {
      "SERPAPI_API_KEY": "TU_CLAVE_AQUI"
    }
  }
}
```

Para producción, configura variables de entorno:
```
API_Keys__Gemini__GEMINI_API_KEY=tu-key
API_Keys__SerpAPI__SERPAPI_API_KEY=tu-key
```

**Rate limiting:** 3 peticiones / 10 segundos (`FixedWindowLimiter`)  
**CORS:** abierto en desarrollo — restringir `AllowAnyOrigin()` en producción.

---

## 🚀 Ejecutar localmente

```bash
# 1. Clona el repositorio
git clone https://github.com/Gobboso/FlightWiseAPI.git

# 2. Copia la plantilla de configuración
cp appsettings.example.json appsettings.json

# 3. Rellena tus claves en appsettings.json

# 4. Ejecuta el proyecto
dotnet run
```

Swagger disponible en: `https://localhost:7190/swagger`

---

## 🧩 Integración con React

```javascript
const [sessionId, setSessionId] = useState(null);

const sendMessage = async (message) => {
  const res = await fetch('https://localhost:7190/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId, message })
  });
  const data = await res.json();
  setSessionId(data.sessionId); // ⚠️ Siempre guardar el sessionId
  return data.response;
};
```

---

## 📈 Potencial de expansión

- Autenticación y perfiles de usuario
- Historial persistente en base de datos
- Alertas de cambios en precios de vuelos
- Módulo de hoteles y experiencias
- Soporte multiidioma
- Despliegue en Azure / AWS con auto-scaling
