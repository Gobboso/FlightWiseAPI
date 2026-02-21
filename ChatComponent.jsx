// ChatComponent.jsx - Ejemplo completo para React

import { useState, useRef, useEffect } from 'react';

const ChatComponent = () => {
  const [sessionId, setSessionId] = useState(() => {
    // Cargar sessionId del localStorage si existe
    return localStorage.getItem('chatSessionId') || null;
  });
  
  const [messages, setMessages] = useState([]);
  const [inputValue, setInputValue] = useState('');
  const [loading, setLoading] = useState(false);
  const messagesEndRef = useRef(null);

  const API_URL = 'https://localhost:7190/api/chat';

  // Auto-scroll a los mensajes más recientes
  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const sendMessage = async (e) => {
    e.preventDefault();
    
    if (!inputValue.trim()) return;

    // Añadir mensaje del usuario
    setMessages(prev => [...prev, { 
      role: 'user', 
      text: inputValue 
    }]);

    setLoading(true);
    const userMessage = inputValue;
    setInputValue('');

    try {
      const response = await fetch(API_URL, {
        method: 'POST',
        headers: { 
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          sessionId: sessionId,
          message: userMessage
        })
      });

      const data = await response.json();

      // IMPORTANTE: Guardar sessionId para próximas solicitudes
      if (!sessionId) {
        setSessionId(data.sessionId);
        localStorage.setItem('chatSessionId', data.sessionId);
      }

      // Añadir respuesta de la IA
      setMessages(prev => [...prev, { 
        role: 'assistant', 
        text: data.response,
        intent: data.intent,
        isFlightSearch: data.isFlightSearch
      }]);

    } catch (error) {
      console.error('Error:', error);
      setMessages(prev => [...prev, { 
        role: 'error', 
        text: 'Error al conectar con el servidor. Intenta nuevamente.' 
      }]);
    } finally {
      setLoading(false);
    }
  };

  const clearSession = async () => {
    if (!sessionId) return;

    try {
      await fetch(`${API_URL}/${sessionId}`, {
        method: 'DELETE'
      });
      
      setSessionId(null);
      setMessages([]);
      localStorage.removeItem('chatSessionId');
    } catch (error) {
      console.error('Error clearing session:', error);
    }
  };

  const getHistory = async () => {
    if (!sessionId) return;

    try {
      const response = await fetch(`${API_URL}/${sessionId}/history`);
      const data = await response.json();
      console.log('Historial completo:', data.history);
    } catch (error) {
      console.error('Error getting history:', error);
    }
  };

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <h1>✈️ Chat de Vuelos</h1>
        {sessionId && (
          <div style={styles.sessionInfo}>
            <span>SessionId: {sessionId.substring(0, 8)}...</span>
            <button onClick={getHistory} style={styles.smallBtn}>
              Ver Historial
            </button>
            <button onClick={clearSession} style={styles.smallBtn}>
              Limpiar Sesión
            </button>
          </div>
        )}
      </div>

      <div style={styles.messagesContainer}>
        {messages.length === 0 && (
          <div style={styles.welcomeMessage}>
            <p>👋 Hola, pregúntame sobre vuelos</p>
            <p style={{ fontSize: '0.9em', color: '#666' }}>
              Ejemplo: "Vuelos de Bogotá a Miami mañana"
            </p>
          </div>
        )}

        {messages.map((msg, idx) => (
          <div 
            key={idx} 
            style={{
              ...styles.message,
              ...(msg.role === 'user' ? styles.userMessage : styles.assistantMessage),
              ...(msg.role === 'error' && styles.errorMessage)
            }}
          >
            <p style={styles.messageText}>{msg.text}</p>
            {msg.isFlightSearch && (
              <small style={{ color: '#999', marginTop: '5px' }}>
                ✈️ Búsqueda de vuelos
              </small>
            )}
          </div>
        ))}

        {loading && (
          <div style={{ ...styles.message, ...styles.assistantMessage }}>
            <div style={styles.typing}>
              <span></span><span></span><span></span>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      <form onSubmit={sendMessage} style={styles.form}>
        <input
          type="text"
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          placeholder="Pregunta sobre vuelos..."
          disabled={loading}
          style={styles.input}
        />
        <button 
          type="submit" 
          disabled={loading || !inputValue.trim()}
          style={styles.submitBtn}
        >
          Enviar
        </button>
      </form>
    </div>
  );
};

const styles = {
  container: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
    backgroundColor: '#f5f5f5',
    fontFamily: 'system-ui, -apple-system, sans-serif'
  },
  header: {
    backgroundColor: '#2c3e50',
    color: 'white',
    padding: '20px',
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    boxShadow: '0 2px 8px rgba(0,0,0,0.1)'
  },
  sessionInfo: {
    display: 'flex',
    gap: '10px',
    alignItems: 'center',
    fontSize: '0.9em'
  },
  smallBtn: {
    padding: '5px 10px',
    fontSize: '0.85em',
    backgroundColor: '#34495e',
    color: 'white',
    border: 'none',
    borderRadius: '4px',
    cursor: 'pointer'
  },
  messagesContainer: {
    flex: 1,
    overflowY: 'auto',
    padding: '20px',
    display: 'flex',
    flexDirection: 'column',
    gap: '10px'
  },
  welcomeMessage: {
    textAlign: 'center',
    color: '#666',
    margin: 'auto',
    padding: '20px'
  },
  message: {
    padding: '12px 16px',
    borderRadius: '8px',
    maxWidth: '80%',
    wordWrap: 'break-word',
    animation: 'slideIn 0.3s ease-in'
  },
  userMessage: {
    alignSelf: 'flex-end',
    backgroundColor: '#3498db',
    color: 'white'
  },
  assistantMessage: {
    alignSelf: 'flex-start',
    backgroundColor: 'white',
    border: '1px solid #ddd'
  },
  errorMessage: {
    backgroundColor: '#e74c3c',
    color: 'white'
  },
  messageText: {
    margin: 0,
    whiteSpace: 'pre-wrap'
  },
  form: {
    display: 'flex',
    gap: '10px',
    padding: '15px',
    backgroundColor: 'white',
    borderTop: '1px solid #ddd'
  },
  input: {
    flex: 1,
    padding: '10px 15px',
    border: '1px solid #ddd',
    borderRadius: '4px',
    fontSize: '1em'
  },
  submitBtn: {
    padding: '10px 20px',
    backgroundColor: '#3498db',
    color: 'white',
    border: 'none',
    borderRadius: '4px',
    cursor: 'pointer',
    fontSize: '1em'
  },
  typing: {
    display: 'flex',
    gap: '4px'
  }
};

export default ChatComponent;
