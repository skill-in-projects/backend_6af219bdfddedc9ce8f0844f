using System.Text.Json;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Asks Gemini for a one-line joke. Each call uses the project's credits.</summary>
[ApiController]
[Route("api/jokes")]
public class JokesController : ControllerBase
{
    private const string GeminiPath = "/v1beta/models/gemini-2.5-flash:generateContent";
    private const string Prompt = "Tell one short, family-friendly joke in one line. Reply with only the joke.";

    /// <summary>GET /api/jokes/random - A one-line joke from Gemini.</summary>
    [HttpGet("random")]
    public async Task<IActionResult> Random()
    {
        if (!GoogleGateway.IsConfigured) return Ok(new { status = "not_configured", message = "Jokes are not available in this app yet." });

        try
        {
            var body = JsonSerializer.Serialize(new { contents = new[] { new { parts = new[] { new { text = Prompt } } } } });
            using var response = await GoogleGateway.PostJsonAsync("gemini", GeminiPath, body);
            var text = await response.Content.ReadAsStringAsync();
            if ((int)response.StatusCode is 402 or 429) return Ok(new { status = "paused", message = "Jokes are paused for now. Please try again later." });
            if (!response.IsSuccessStatusCode) return Ok(new { status = "error", message = "Could not get a joke right now. Please try again." });

            using var doc = JsonDocument.Parse(text);
            var joke = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim();
            if (string.IsNullOrEmpty(joke)) return Ok(new { status = "error", message = "Could not get a joke right now. Please try again." });
            return Ok(new { status = "ok", joke });
        }
        catch (Exception)
        {
            return Ok(new { status = "error", message = "Could not get a joke right now. Please try again." });
        }
    }
}
