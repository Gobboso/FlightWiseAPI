# 🔧 Guía de Troubleshooting - FlightWise API

## 🚨 Error "Bad Request" en búsqueda de vuelos

### Problema
La IA responde: "error de solicitud" o "Bad Request" al buscar vuelos.

### Causas Comunes y Soluciones

#### 1. **API Key Inválida**
**Síntomas:** Siempre falla la búsqueda
**Solución:**
- Verifica tu API key en `appsettings.json`
- Prueba la key directamente en: https://serpapi.com/search.json?engine=google_flights&departure_id=BOG&arrival_id=MDE&outbound_date=2026-03-15&api_key=TU_API_KEY

#### 2. **Formato de Fecha Incorrecto**
**Síntomas:** Falla con fechas específicas
**Solución:**
- SerpAPI requiere formato: `YYYY-MM-DD` (ej: `2026-03-15`)
- **No usar fechas pasadas** - Estamos en 2026, usa fechas de 2026 en adelante
- Verificar que la IA extraiga correctamente la fecha con el año correcto

#### 3. **Códigos IATA Incorrectos**
**Síntomas:** Falla con ciudades específicas
**Solución:**
```http
GET /api/test/airport-code?city=Barranquilla
```
Respuesta esperada:
```json
{
  "city": "Barranquilla",
  "code": "BAQ"
}
```

Si el código no es correcto, añádelo a `Utils/AirportMapper.cs`

#### 4. **Tipo de Vuelo No Especificado** ✅ SOLUCIONADO
**Síntomas:** Error: `return_date is required if type is 1 (Round trip)`
**Causa:** SerpAPI requiere el parámetro `type`:
- `type=1` = Ida y vuelta (requiere `return_date`)
- `type=2` = Solo ida (no requiere `return_date`)

**Solución:** Ya está implementada automáticamente en el código. El sistema detecta si proporcionaste fecha de regreso y configura el tipo correcto.

#### 5. **Límite de Requests de SerpAPI**
**Síntomas:** Funciona al inicio, luego falla
**Solución:**
- Cuenta gratuita: 100 búsquedas/mes
- Revisa tu dashboard en: https://serpapi.com/dashboard
- Considera plan de pago si necesitas más

### 🧪 Cómo Diagnosticar

#### Paso 1: Probar SerpAPI Directamente
```http
# Solo ida
GET /api/test/flights?origin=BOG&destination=BAQ&date=2026-03-15

# Ida y vuelta (añade returnDate cuando esté implementado)
```

**Respuesta exitosa:**
```json
{
  "search_metadata": { ... },
  "best_flights": [ ... ],
  "other_flights": [ ... ]
}
```

**Respuesta con error:**
```json
{
  "error": true,
  "message": "Error de SerpAPI: Invalid API key",
  "statusCode": 400
}
```

#### Paso 2: Verificar Logs
Revisa la consola de Visual Studio (Output window) para ver:
```
Buscando vuelos: BOG -> BAQ, Fecha: 2026-03-15, Tipo: Solo ida
URL: https://serpapi.com/search.json?engine=google_flights&departure_id=BOG...
```

#### Paso 3: Verificar Mapeo de Ciudades
```http
GET /api/test/airport-code?city=Bogotá
GET /api/test/airport-code?city=Barranquilla
GET /api/test/airport-code?city=Miami
```

### 🔍 Errores Específicos de SerpAPI

| Error | Causa | Solución |
|-------|-------|----------|
| `Invalid API key` | API key incorrecta | Verificar en appsettings.json |
| `Invalid parameters` | Parámetros mal formados | Verificar formato de fecha (año 2026) |
| `return_date is required if type is 1` | Falta fecha de regreso en ida y vuelta | ✅ Ya solucionado automáticamente |
| `Rate limit exceeded` | Demasiadas requests | Esperar o actualizar plan |
| `No flights found` | No hay vuelos disponibles | Normal, probar otras fechas |

### 📝 Formato Correcto de Parámetros

```
departure_id: Código IATA de 3 letras (BOG, MDE, MIA)
arrival_id: Código IATA de 3 letras
outbound_date: YYYY-MM-DD (fecha futura, año 2026 o posterior)
return_date: YYYY-MM-DD (opcional, solo para ida y vuelta)
type: 1 (ida y vuelta) o 2 (solo ida) - ✅ Se configura automáticamente
adults: Número entero (1-9)
currency: USD, COP, EUR, etc.
```

### 🛠️ Solución Rápida: Probar con Datos Conocidos

```http
# Solo ida
POST /api/chat
{
  "message": "Vuelos de Bogotá a Medellín para el 15 de marzo de 2026"
}

# Ida y vuelta
POST /api/chat
{
  "message": "Vuelos de Bogotá a Medellín el 15 de marzo, regreso el 20 de marzo de 2026"
}
```

### 📞 Verificar Status de SerpAPI

- Dashboard: https://serpapi.com/dashboard
- Status: https://serpapi.com/status
- Docs: https://serpapi.com/google-flights-api

### 💡 Tips

1. **Siempre usa fechas futuras**: SerpAPI no busca vuelos en el pasado (estamos en 2026)
2. **Códigos IATA válidos**: Verifica en https://www.iata.org/en/publications/directories/code-search/
3. **Revisa límites**: Cuenta gratuita = 100 búsquedas/mes
4. **Usa el logger**: Revisa los logs en la consola para ver exactamente qué URL se está llamando
5. **Ida y vuelta**: La IA debe detectar ambas fechas, o pídelas explícitamente
6. **Año actual**: El sistema usa automáticamente el año 2026

### 🐛 Si Sigue Sin Funcionar

1. Revisa los logs en la consola de Visual Studio
2. Copia la URL que aparece en los logs (sin la API key)
3. Pruébala directamente en tu navegador agregando `&api_key=TU_KEY`
4. Si funciona en el navegador pero no en la API, es un problema de código
5. Si no funciona en el navegador, es un problema con SerpAPI

### 📊 Logs Útiles

Busca en Output window (Visual Studio):
```
[Information] Buscando vuelos: BOG -> BAQ, Fecha: 2026-03-15, Tipo: Solo ida
[Information] URL: https://serpapi.com/search.json?...
[Information] Vuelos encontrados exitosamente
```

O errores:
```
[Error] Error de SerpAPI: Status 400, Response: {...}
```

### ✅ Errores Ya Solucionados

- ✅ `return_date is required if type is 1` - El sistema ahora envía `type=2` para solo ida
- ✅ SessionId obligatorio - Ahora se genera automáticamente
- ✅ Mapeo de ciudades - Incluye más de 60 ciudades principales
- ✅ Año incorrecto - El sistema usa automáticamente 2026
