# ✈️ FlightWiseAPI

Backend de una API inteligente de búsqueda de vuelos con asistente conversacional basado en IA. Desarrollado en **ASP.NET Core (.NET 8)**, integra **Google Gemini AI** para procesamiento de lenguaje natural y **SerpAPI (Google Flights)** para la búsqueda de vuelos en tiempo real.


---

## 🧠 ¿Qué hace este programa?

FlightWiseAPI actúa como el cerebro de un chatbot de viajes. El usuario escribe en lenguaje natural (ej: *"Quiero vuelos de Bogotá a Madrid para el 20 de marzo"*) y la API:

1. **Detecta la intención** del mensaje usando Gemini AI (¿busca vuelos o es una pregunta general?).
2. **Extrae los datos** relevantes: origen, destino, fecha de salida, fecha de regreso, número de adultos.
3. **Resuelve los códigos IATA** del aeropuerto (primero por diccionario estático, luego por IA si no se encuentra).
4. **Busca vuelos reales** en USD y COP mediante SerpAPI (Google Flights).
5. **Genera una respuesta natural** con hasta 3 opciones de vuelo, precios, duración y un link directo a Google Flights.
6. **Mantiene el contexto** de la conversación por sesión para respuestas coherentes.

---

## 🏗️ Arquitectura del proyecto

```
FlightWiseAPI/
├── Controllers/
│   ├── ChatController.cs        # Endpoint principal del chat (/api/chat)
│   ├── GeminiAIController.cs    # Consulta directa a Gemini (/api/gemini)
│   └── TestController.cs        # Endpoints de prueba (/api/test)
├── Services/
│   ├── GeminiAIService.cs       # Integración con Google Gemini AI
│   └── FlightsService.cs        # Integración con SerpAPI (Google Flights)
├── Memory/
│   └── ChatMemory.cs            # Gestión de historial de conversación en memoria
├── Models/
│   ├── ChatDTO.cs               # Modelo de entrada del chat
│   ├── ChatResponseDTO.cs       # Modelo de respuesta del chat
│   └── FlightIntentDTO.cs       # Modelo de intención extraída por la IA
├── Utils/
│   └── AirportMapper.cs         # Diccionario estático ciudad → código IATA
├── Program.cs                   # Configuración y arranque de la aplicación
└── appsettings.json             # Configuración y claves de API
```

---

## 🔌 Endpoints

### `POST /api/chat` — Chat principal
Endpoint principal. Recibe un mensaje del usuario y devuelve una respuesta inteligente.

**Request:**
```json
{
  "sessionId": "abc-123",
  "message": "Quiero vuelos de Bogotá a Miami para el 15 de abril"
}
```
> `sessionId` es opcional en el primer mensaje. A partir del segundo es obligatorio para mantener el contexto.

**Response:**
```json
{
  "sessionId": "abc-123",
  "response": "✈️ Encontré estas opciones...",
  "intent": "ask_flights",
  "isFlightSearch": true
}
```

| Campo | Descripción |
|---|---|
| `sessionId` | ID de sesión para mantener el historial de conversación |
| `response` | Texto de respuesta generado por la IA |
| `intent` | `ask_flights` si buscó vuelos, `chat` si fue conversación general, `error` si falló |
| `isFlightSearch` | `true` si se realizó una búsqueda real de vuelos |

---

### `POST /api/gemini` — Consulta directa a Gemini
Permite enviar un prompt libre directamente al modelo de IA.

**Request:**
```json
{
  "prompt": "¿Cuál es la capital de Australia?"
}
```

**Response:**
```json
{
  "responseText": "La capital de Australia es Canberra."
}
```

---

### `GET /api/test/flights` — Prueba de búsqueda de vuelos
Endpoint de desarrollo para probar SerpAPI directamente.

```
GET /api/test/flights?origin=BOG&destination=MIA&date=2026-04-15
```

---

### `GET /api/test/airport-code` — Prueba del mapeador de aeropuertos
Verifica la resolución de nombre de ciudad a código IATA.

```
GET /api/test/airport-code?city=Medellín
```

**Response:**
```json
{
  "city": "Medellín",
  "code": "MDE"
}
```

---

## ⚙️ Servicios internos

### `GeminiAIService`
Centraliza toda la comunicación con la API de Google Gemini (`gemini-3-flash-preview`).

| Método | Descripción |
|---|---|
| `AskGemini(prompt)` | Envía un prompt al modelo y devuelve texto. Incluye reintentos automáticos (máx. 3) con backoff exponencial ante errores 429 o 5xx. Timeout configurado en 30 segundos. |
| `DetectIntent(message, history)` | Analiza el mensaje del usuario y devuelve un JSON con la intención detectada y los datos extraídos (origen, destino, fecha, etc.). |
| `GenerateFlightResponse(message, flightData, history)` | Genera una respuesta amigable con los datos de vuelos encontrados (máx. 3 opciones). |
| `GenerateChatResponse(message, history)` | Genera una respuesta conversacional general para preguntas que no son de vuelos. |
| `ResolveAirportCode(cityName)` | Pide a Gemini el código IATA de 3 letras de una ciudad cuando no está en el diccionario local. |

### `FlightsService`
Gestiona las búsquedas de vuelos contra SerpAPI (Google Flights).

| Método | Descripción |
|---|---|
| `BuscarVuelos(origen, destino, fechaSalida, fechaRegreso, adultos)` | Realiza búsquedas paralelas en **USD** y **COP** y combina los resultados. Soporta vuelos de ida y de ida y vuelta. |

### `ChatMemory` (estático)
Gestión de historial conversacional **en memoria** (no persiste entre reinicios del servidor).

| Método | Descripción |
|---|---|
| `AddMessage(sessionId, role, message)` | Agrega un mensaje al historial de la sesión. |
| `GetFormattedHistory(sessionId, maxMessages)` | Devuelve los últimos N mensajes formateados como texto para enviar a la IA. |
| `ClearSession(sessionId)` | Limpia el historial de una sesión. |

### `AirportMapper` (estático)
Diccionario de más de 70 ciudades mapeadas a su código IATA (Colombia, EE.UU., Europa, Latinoamérica, Asia, Oceanía). Si no encuentra la ciudad, devuelve el texto original y `GeminiAIService` lo resuelve mediante IA.

---

## 📦 Modelos (DTOs)

### `ChatDto` — Entrada
```
string? SessionId   // ID de sesión (null en el primer mensaje)
string  Message     // Mensaje del usuario
```

### `ChatResponseDto` — Salida
```
string SessionId      // ID de sesión a mantener en el cliente
string Response       // Respuesta de la IA
string Intent         // "ask_flights" | "chat" | "error"
bool   IsFlightSearch // Si se realizó búsqueda real de vuelos
```

### `FlightIntentDTO` — Intención extraída (uso interno)
```
string       intent      // "ask_flights" | "chat"
string       origin      // Ciudad de origen
string       destination // Ciudad de destino
string       date        // Fecha de salida (YYYY-MM-DD)
string       returnDate  // Fecha de regreso (YYYY-MM-DD), si aplica
int          adults      // Número de adultos (default: 1)
List<string> missing     // Campos faltantes que la IA necesita preguntar
```

---

## 🔒 Rate Limiting

Configurado con **Fixed Window Limiter**:

| Parámetro | Valor |
|---|---|
| Límite de peticiones | 3 por ventana |
| Ventana de tiempo | 10 segundos |
| Cola | Sin cola (rechaza inmediatamente) |
| Orden de procesamiento | OldestFirst |

---

## 🌐 CORS

Configurado con política `"frontend"` que permite cualquier origen, método y cabecera. Pensado para desarrollo local con un frontend desacoplado (React, Vue, etc.).

> ⚠️ Para producción, reemplazar `AllowAnyOrigin()` por los dominios específicos del frontend.

---

## 🔑 Configuración (`appsettings.json`)

```json
{
  "API_Keys": {
    "Gemini": {
      "GEMINI_API_KEY": "TU_CLAVE_GEMINI"
    },
    "SerpAPI": {
      "SERPAPI_API_KEY": "TU_CLAVE_SERPAPI"
    }
  }
}
```

> ⚠️ **Nunca subas `appsettings.json` con claves reales a un repositorio público.** Usa variables de entorno o `dotnet user-secrets` en desarrollo.

Para deploy, configura las variables de entorno así:
```
API_Keys__Gemini__GEMINI_API_KEY=tu-key
API_Keys__SerpAPI__SERPAPI_API_KEY=tu-key
```

---

## 🔄 Flujo completo de una petición de vuelos

```
Cliente
  │
  ├─► POST /api/chat { message, sessionId }
  │         │
  │         ├─► GeminiAI.DetectIntent(message, history)
  │         │         └─► { intent: "ask_flights", origin, destination, date }
  │         │
  │         ├─► AirportMapper.GetCode(origin / destination)
  │         │         └─► "BOG", "MIA"  (si no está en diccionario → GeminiAI.ResolveAirportCode)
  │         │
  │         ├─► FlightsService.BuscarVuelos(BOG, MIA, fecha)
  │         │         ├─► SerpAPI Google Flights (USD)
  │         │         └─► SerpAPI Google Flights (COP)
  │         │                   └─► JSON combinado USD + COP
  │         │
  │         └─► GeminiAI.GenerateFlightResponse(flightData)
  │                   └─► Texto amigable + link Google Flights
  │
  └─◄ { sessionId, response, intent, isFlightSearch }
```

---

## 🧩 Integración con React

El archivo `ChatComponent.jsx` incluye un componente React funcional listo para usar que:

- Gestiona el `sessionId` en `localStorage` para persistir la sesión entre recargas.
- Muestra el historial de mensajes con auto-scroll.
- Maneja estados de carga y errores.
- Envía y recibe mensajes al endpoint `/api/chat`.

**Ejemplo mínimo:**
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

## 🛠️ Tecnologías utilizadas

| Tecnología | Uso |
|---|---|
| ASP.NET Core (.NET 8) | Framework principal de la API |
| C# 12 | Lenguaje de programación |
| Google Gemini AI (`gemini-3-flash-preview`) | Detección de intención, resolución de códigos IATA, generación de respuestas |
| SerpAPI (Google Flights) | Búsqueda de vuelos reales en tiempo real |
| `System.Threading.RateLimiting` | Control de tasa de peticiones |
| `Microsoft.AspNetCore.RateLimiting` | Middleware de rate limiting |
| Swagger / OpenAPI | Documentación interactiva de la API (solo en desarrollo) |
| React (frontend externo) | Integración mediante `ChatComponent.jsx` |
