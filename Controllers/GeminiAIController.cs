using Microsoft.AspNetCore.Mvc;
using FlightWiseAPI.Services;

namespace FlightWiseAPI.Controllers
{
    [ApiController]
    [Route("api/gemini")]
    public class GeminiAIController : ControllerBase
    {
        private readonly GeminiAIService _gemini;

        public GeminiAIController(GeminiAIService geminiResponse)
        {
            _gemini = geminiResponse;
        }

        [HttpPost]
        public async Task<IActionResult> AskGemini([FromBody] PromptDto dto)
        {
            var responseText = await _gemini.AskGemini(dto.Prompt);
            return Ok(new { responseText });
        }
    }

    public class PromptDto
    {
        public required string Prompt { get; set; }
    }
}
