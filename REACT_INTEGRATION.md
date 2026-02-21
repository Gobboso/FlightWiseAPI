# 📱 Guía de Integración con React

## 🔑 Concepto Clave: SessionId

El **sessionId** es la clave para mantener el contexto. **DEBE guardarse** entre solicitudes.

## ⚠️ PROBLEMA COMÚN

```javascript
// ❌ MAL - Cada solicitud crea nuevo sessionId
const sendMessage = async (message) => {
  const response = await fetch('/api/chat', {
    method: 'POST',
    body: JSON.stringify({ message }) // Sin sessionId
  });
};
```

## ✅ SOLUCIÓN CORRECTA

```javascript
// ✅ BIEN - Guardar sessionId en estado
import { useState } from 'react';

function ChatComponent() {
  const [sessionId, setSessionId] = useState(null);
  const [messages, setMessages] = useState([]);

  const sendMessage = async (message) => {
    try {
      const response = await fetch('https://localhost:7190/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: sessionId, // IMPORTANTE: incluir sessionId
          message: message
        })
      });

      const data = await response.json();

      // GUARDAR el sessionId retornado para próximas solicitudes
      if (!sessionId) {
        setSessionId(data.sessionId);
      }

      // Añadir mensajes a la conversación
      setMessages([
        ...messages,
        { role: 'user', text: message },
        { role: 'assistant', text: data.response }
      ]);

    } catch (error) {
      console.error('Error:', error);
    }
  };

  return (
    <div>
      <h2>Chat de Vuelos</h2>
      <div className="messages">
        {messages.map((msg, i) => (
          <div key={i} className={msg.role}>
            {msg.text}
          </div>
        ))}
      </div>
      <input 
        type="text" 
        placeholder="Pregunta sobre vuelos..."
        onKeyPress={(e) => {
          if (e.key === 'Enter') {
            sendMessage(e.target.value);
            e.target.value = '';
          }
        }}
      />
    </div>
  );
}

export default ChatComponent;
```

## 📊 Estructura de Respuesta

```json
{
  "sessionId": "abc-123-def",  // ← GUARDA ESTO
  "response": "Encontré estos vuelos...",
  "intent": "ask_flights",
  "isFlightSearch": true
}
```

## 🔄 Flujo Completo

### Primera solicitud (sin sessionId)
```javascript
POST /api/chat
{
  "message": "Vuelos de Bogotá a Miami"
}

Respuesta:
{
  "sessionId": "uuid-generado",
  "response": "¿Qué fecha te gustaría?"
  "intent": "ask_flights",
  "isFlightSearch": false
}
```

### Guardar el sessionId
```javascript
const sessionId = "uuid-generado"; // Del paso anterior
```

### Segunda solicitud (CON sessionId)
```javascript
POST /api/chat
{
  "sessionId": "uuid-generado",  // ← MISMO sessionId
  "message": "Para el 15 de marzo"
}

Respuesta:
{
  "sessionId": "uuid-generado",  // ← MISMO sessionId
  "response": "Encontré 5 vuelos...",
  "intent": "ask_flights",
  "isFlightSearch": true
}
```

## 💾 Guardar SessionId en LocalStorage (Recomendado)

```javascript
const sendMessage = async (message) => {
  let sessionId = localStorage.getItem('chatSessionId');

  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      sessionId: sessionId,
      message: message
    })
  });

  const data = await response.json();

  // Guardar sessionId para próximas solicitudes
  if (!sessionId) {
    localStorage.setItem('chatSessionId', data.sessionId);
  }

  return data;
};
```

## 🧹 Limpiar Sesión

```javascript
const clearSession = async () => {
  const sessionId = localStorage.getItem('chatSessionId');
  
  await fetch(`/api/chat/${sessionId}`, {
    method: 'DELETE'
  });

  localStorage.removeItem('chatSessionId');
};
```

## 📝 Obtener Historial

```javascript
const getHistory = async () => {
  const sessionId = localStorage.getItem('chatSessionId');
  
  const response = await fetch(`/api/chat/${sessionId}/history`);
  const data = await response.json();
  
  console.log(data.history); // Todos los mensajes de la sesión
};
```

## 🎯 Checklist de Integración

- [ ] Guardas el `sessionId` de la primera respuesta
- [ ] Envías el mismo `sessionId` en las próximas solicitudes
- [ ] Usas `localStorage` o estado global para persistencia
- [ ] Manejaste las fechas correctamente (futuras)
- [ ] Capturaste el campo `isFlightSearch` si necesitas diferenciar tipos de respuesta

## ⚡ Ejemplo Completo Minimalista

```javascript
const [sessionId, setSessionId] = useState(null);

const chat = async (msg) => {
  const res = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId, message: msg })
  });
  const data = await res.json();
  setSessionId(data.sessionId); // Guardar o actualizar
  return data.response;
};
```

## 🚀 Prueba Rápida en Terminal/Postman

```bash
# Primera solicitud
curl -X POST https://localhost:7190/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Vuelos de Bogotá a Miami"}'

# Respuesta: obtiene sessionId

# Segunda solicitud CON el sessionId
curl -X POST https://localhost:7190/api/chat \
  -H "Content-Type: application/json" \
  -d '{"sessionId": "abc-123", "message": "Para mañana"}'
```

## ✅ Verificar Que Funciona

Si el contexto se mantiene, deberías ver:

```
Usuario: "Vuelos de Bogotá a Miami"
IA: "¿Qué fecha te gustaría?"

Usuario: "Para mañana"
IA: "Encontré X vuelos de Bogotá a Miami para [mañana]..."
      ↑ Nota: Recuerda el origen y destino del mensaje anterior
```

Si dice "¿Vuelos de dónde a dónde?" en la segunda solicitud, es porque **no está guardando el sessionId correctamente**.

## 🐛 Debugging

```javascript
// Ver qué sessionId estás enviando
console.log('Enviando sessionId:', sessionId);

// Ver qué respuesta recibiste
console.log('Respuesta:', data);

// Verificar historial
fetch(`/api/chat/${sessionId}/history`)
  .then(r => r.json())
  .then(data => console.log('Historial:', data.history));
```
