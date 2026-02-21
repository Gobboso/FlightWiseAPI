namespace FlightWiseAPI.Memory
{
    public class ChatMessage
    {
        public string Role { get; set; } // "user" o "assistant"
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public static class ChatMemory
    {
        public static Dictionary<string, List<ChatMessage>> Conversations = new();

        public static void AddMessage(string sessionId, string role, string message)
        {
            if (!Conversations.ContainsKey(sessionId))
                Conversations[sessionId] = new List<ChatMessage>();

            Conversations[sessionId].Add(new ChatMessage
            {
                Role = role,
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public static string GetFormattedHistory(string sessionId, int maxMessages = 10)
        {
            if (!Conversations.ContainsKey(sessionId))
                return "";

            var messages = Conversations[sessionId]
                .TakeLast(maxMessages)
                .Select(m => $"{m.Role}: {m.Message}")
                .ToList();

            return string.Join("\n", messages);
        }

        public static void ClearSession(string sessionId)
        {
            if (Conversations.ContainsKey(sessionId))
                Conversations[sessionId].Clear();
        }
    }
}
