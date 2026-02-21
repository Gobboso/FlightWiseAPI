namespace FlightWiseAPI.Utils
{
    public static class AirportMapper
    {
        private static readonly Dictionary<string, string> CityToAirportCode = new()
        {
            // Colombia
            { "bogota", "BOG" },
            { "bogotá", "BOG" },
            { "medellin", "MDE" },
            { "medellín", "MDE" },
            { "cali", "CLO" },
            { "cartagena", "CTG" },
            { "barranquilla", "BAQ" },
            { "bucaramanga", "BGA" },
            { "pereira", "PEI" },
            { "santa marta", "SMR" },
            { "cucuta", "CUC" },
            { "cúcuta", "CUC" },
            
            // Estados Unidos
            { "new york", "JFK" },
            { "nueva york", "JFK" },
            { "los angeles", "LAX" },
            { "chicago", "ORD" },
            { "miami", "MIA" },
            { "houston", "IAH" },
            { "san francisco", "SFO" },
            { "washington", "IAD" },
            { "boston", "BOS" },
            { "atlanta", "ATL" },
            { "dallas", "DFW" },
            { "orlando", "MCO" },
            { "seattle", "SEA" },
            { "las vegas", "LAS" },
            
            // México
            { "mexico", "MEX" },
            { "méxico", "MEX" },
            { "ciudad de mexico", "MEX" },
            { "guadalajara", "GDL" },
            { "cancun", "CUN" },
            { "cancún", "CUN" },
            { "monterrey", "MTY" },
            
            // Europa
            { "madrid", "MAD" },
            { "barcelona", "BCN" },
            { "paris", "CDG" },
            { "parís", "CDG" },
            { "london", "LHR" },
            { "londres", "LHR" },
            { "rome", "FCO" },
            { "roma", "FCO" },
            { "amsterdam", "AMS" },
            { "berlin", "BER" },
            { "berlín", "BER" },
            { "lisbon", "LIS" },
            { "lisboa", "LIS" },
            { "milan", "MXP" },
            { "milán", "MXP" },
            { "frankfurt", "FRA" },
            { "zurich", "ZRH" },
            
            // América Latina
            { "buenos aires", "EZE" },
            { "santiago", "SCL" },
            { "lima", "LIM" },
            { "sao paulo", "GRU" },
            { "são paulo", "GRU" },
            { "rio de janeiro", "GIG" },
            { "quito", "UIO" },
            { "panama", "PTY" },
            { "panamá", "PTY" },
            { "san jose", "SJO" },
            { "san josé", "SJO" },
            
            // Asia
            { "tokyo", "NRT" },
            { "tokio", "NRT" },
            { "beijing", "PEK" },
            { "pekin", "PEK" },
            { "shanghai", "PVG" },
            { "dubai", "DXB" },
            { "singapore", "SIN" },
            { "singapur", "SIN" },
            { "hong kong", "HKG" },
            { "bangkok", "BKK" },
            { "seoul", "ICN" },
            { "seúl", "ICN" },
            
            // Oceanía
            { "sydney", "SYD" },
            { "sídney", "SYD" },
            { "melbourne", "MEL" },
            { "auckland", "AKL" }
        };

        public static string GetCode(string cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                return "";

            var normalized = cityName.ToLower().Trim();
            
            // Si ya es un código IATA (3 letras mayúsculas), devolverlo
            if (cityName.Length == 3 && cityName == cityName.ToUpper())
                return cityName;
            
            // Buscar coincidencia exacta
            if (CityToAirportCode.TryGetValue(normalized, out var code))
                return code;
            
            // Buscar coincidencia parcial
            var partialMatch = CityToAirportCode
                .FirstOrDefault(x => x.Key.Contains(normalized) || normalized.Contains(x.Key));
            
            if (!string.IsNullOrEmpty(partialMatch.Value))
                return partialMatch.Value;
            
            // Si no encuentra, devolver el input (la IA lo resolverá)
            return cityName;
        }
    }
}
