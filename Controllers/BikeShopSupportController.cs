using System.Text.Json;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>A support agent for a bike shop: answers questions about repairs and opening hours. Each message uses the project's credits.</summary>
[ApiController]
[Route("api/bike-shop/chat")]
public class BikeShopSupportController : ControllerBase
{
    private const string GeminiPath = "/v1beta/models/gemini-3.5-flash-lite:generateContent";
    private const int MaxMessageLength = 1000;
    private const int MaxHistory = 10;
    private static readonly string SystemPrompt =
        "You are the customer support agent of a bike shop. Answer customers' questions about repairs and opening hours " +
        "in a friendly, short way, in plain words. Answer only from the shop information below. If the answer is not " +
        "in that information, say you are not sure and suggest the customer visit the shop during opening hours. " +
        "Never invent prices, times or policies.\n\nShop information:\n" + BikeShopInfo.Text;

    public class ChatTurn
    {
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public class ChatRequest
    {
        public string Message { get; set; } = "";
        public List<ChatTurn> History { get; set; } = new();
    }

    /// <summary>POST /api/bike-shop/chat - One reply from Gemini, given the new question and the earlier turns.</summary>
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ChatRequest request)
    {
        var question = (request.Message ?? "").Trim();
        if (question.Length == 0) return Ok(new { status = "error", message = "Please type a question first." });
        if (question.Length > MaxMessageLength) return Ok(new { status = "error", message = "That message is too long. Please shorten it." });
        if (!GoogleGateway.IsConfigured) return Ok(new { status = "not_configured", message = "The shop assistant is not available in this app yet." });

        // Only the last few turns are sent, and only user and model turns are kept
        var turns = (request.History ?? new List<ChatTurn>())
            .Where(turn => turn.Role is "user" or "model" && !string.IsNullOrWhiteSpace(turn.Text))
            .TakeLast(MaxHistory)
            .Select(turn => new { role = turn.Role, parts = new[] { new { text = turn.Text } } });
        var contents = turns.Append(new { role = "user", parts = new[] { new { text = question } } }).ToArray();

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                system_instruction = new { parts = new[] { new { text = SystemPrompt } } },
                contents
            });
            using var response = await GoogleGateway.PostJsonAsync("gemini", GeminiPath, body);
            var text = await response.Content.ReadAsStringAsync();

            if ((int)response.StatusCode is 402 or 429)
            {
                var code = ReadCode(text);
                if (code == "out_of_credits") return Ok(new { status = "paused", message = "The shop assistant is paused for now. Please try again later." });
                if (code is "daily_credit_limit" or "daily_call_limit" or "run_call_limit") return Ok(new { status = "paused", message = "The shop assistant has reached today's limit. Please try again tomorrow." });
                return Ok(new { status = "unavailable", message = "The shop assistant is unavailable right now. Please try again later." });
            }
            if (!response.IsSuccessStatusCode) return Ok(new { status = "error", message = "The shop assistant could not answer right now. Please try again." });

            using var doc = JsonDocument.Parse(text);
            var reply = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrEmpty(reply)) return Ok(new { status = "error", message = "The shop assistant could not answer right now. Please try again." });
            return Ok(new { status = "ok", reply });
        }
        catch (Exception)
        {
            return Ok(new { status = "error", message = "The shop assistant could not answer right now. Please try again." });
        }
    }

    private static string? ReadCode(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
