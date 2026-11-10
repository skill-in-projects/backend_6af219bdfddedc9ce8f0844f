using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Suggests three dinner ideas from the ingredients in the fridge. Each call uses the project's credits.</summary>
[ApiController]
[Route("api/dinner")]
public class DinnerController : ControllerBase
{
    private const string GeminiPath = "/v1beta/models/gemini-3.5-flash-lite:generateContent";
    private const int MaxIngredientsLength = 300;

    /// <summary>GET /api/dinner/ideas?ingredients=... - Three dinner ideas from Gemini.</summary>
    [HttpGet("ideas")]
    public async Task<IActionResult> Ideas([FromQuery] string? ingredients)
    {
        var items = (ingredients ?? "").Trim();
        if (items.Length == 0) return Ok(new { status = "error", message = "Please tell me what is in your fridge." });
        if (items.Length > MaxIngredientsLength) return Ok(new { status = "error", message = "That list is too long. Please shorten it." });
        if (!GoogleGateway.IsConfigured) return Ok(new { status = "not_configured", message = "Dinner ideas are not available in this app yet." });

        try
        {
            var prompt = $"Suggest 3 dinner ideas that use some of these fridge ingredients: {items}. " +
                         "Reply with exactly 3 lines, one short dinner idea per line, with no numbering and no extra text.";
            var body = JsonSerializer.Serialize(new { contents = new[] { new { parts = new[] { new { text = prompt } } } } });
            using var response = await GoogleGateway.PostJsonAsync("gemini", GeminiPath, body);
            var text = await response.Content.ReadAsStringAsync();

            if ((int)response.StatusCode is 402 or 429)
            {
                var code = ReadCode(text);
                if (code == "out_of_credits") return Ok(new { status = "paused", message = "Dinner ideas are paused because the project has run out of credits for now." });
                if (code is "daily_credit_limit" or "daily_call_limit" or "run_call_limit") return Ok(new { status = "paused", message = "Dinner ideas have reached today's limit. Please try again tomorrow." });
                return Ok(new { status = "unavailable", message = "Dinner ideas are unavailable right now. Please try again later." });
            }
            if (!response.IsSuccessStatusCode) return Ok(new { status = "error", message = "Could not get dinner ideas right now. Please try again." });

            using var doc = JsonDocument.Parse(text);
            var answer = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
            var ideas = answer
                .Split('\n')
                .Select(line => Regex.Replace(line.Trim(), @"^(\d+[.)]|[-*•])\s*", ""))
                .Where(line => line.Length > 0)
                .Take(3)
                .ToArray();
            if (ideas.Length == 0) return Ok(new { status = "error", message = "Could not get dinner ideas right now. Please try again." });
            return Ok(new { status = "ok", ideas });
        }
        catch (Exception)
        {
            return Ok(new { status = "error", message = "Could not get dinner ideas right now. Please try again." });
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
