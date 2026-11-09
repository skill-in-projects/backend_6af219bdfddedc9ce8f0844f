using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Backend.Controllers;

/// <summary>Skill-in platform file: a test of each Google API. Each test except /status uses the project's credits.</summary>
[ApiController]
[Route("api/google")]
public class GoogleApiController : ControllerBase
{
    private const string GeminiPath = "/v1beta/models/gemini-2.5-flash:generateContent";

    /// <summary>GET /api/google/status - Whether Google APIs are connected (no external call, no credits).</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(new
    {
        configured = GoogleGateway.IsConfigured,
        message = GoogleGateway.IsConfigured ? "Google APIs are connected through the platform. Each call uses credits." : "Google APIs are not connected yet.",
    });

    [HttpGet("health")]
    public Task<IActionResult> Health() => Gemini();

    /// <summary>GET /api/google/gemini - Test Gemini API.</summary>
    [HttpGet("gemini")]
    public Task<IActionResult> Gemini() => Run("Gemini", async () =>
    {
        var body = JsonSerializer.Serialize(new { contents = new[] { new { parts = new[] { new { text = "Reply with exactly: OK" } } } } });
        return await GoogleGateway.PostJsonAsync("gemini", GeminiPath, body);
    }, _ => null);

    /// <summary>GET /api/google/geocoding - Test Geocoding API (addresses to coordinates).</summary>
    [HttpGet("geocoding")]
    public Task<IActionResult> Geocoding() => Run("Geocoding",
        () => GoogleGateway.GetAsync("maps", "/maps/api/geocode/json?address=Times+Square+New+York"), MapsStatusError);

    /// <summary>GET /api/google/maps - Test Static Maps (a map as an image).</summary>
    [HttpGet("maps")]
    public Task<IActionResult> Maps() => Run("Maps",
        () => GoogleGateway.GetAsync("maps", "/maps/api/staticmap?center=Times+Square+New+York&zoom=14&size=300x200"), _ => null);

    /// <summary>GET /api/google/directions - Test Directions API (routes between addresses).</summary>
    [HttpGet("directions")]
    public Task<IActionResult> Directions()
    {
        var origin = Uri.EscapeDataString("Times Square, New York, NY");
        var destination = Uri.EscapeDataString("Central Park, New York, NY");
        return Run("Directions", () => GoogleGateway.GetAsync("maps", $"/maps/api/directions/json?origin={origin}&destination={destination}"), MapsStatusError);
    }

    /// <summary>GET /api/google/places - Test Places API (New): search places.</summary>
    [HttpGet("places")]
    public Task<IActionResult> Places() => Run("Places",
        () => GoogleGateway.PostJsonAsync("places", "/v1/places:searchText", JsonSerializer.Serialize(new { textQuery = "coffee" }), "places.id"), _ => null);

    /// <summary>GET /api/google/speech-to-text - Test Cloud Speech-to-Text API with minimal audio.</summary>
    [HttpGet("speech-to-text")]
    public Task<IActionResult> SpeechToText()
    {
        var body = JsonSerializer.Serialize(new
        {
            config = new { encoding = "LINEAR16", sampleRateHertz = 16000, languageCode = "en-US" },
            audio = new { content = Convert.ToBase64String(new byte[3200]) },
        });
        return Run("SpeechToText", () => GoogleGateway.PostJsonAsync("speech", "/v1/speech:recognize", body), _ => null);
    }

    private static string? MapsStatusError(string responseText)
    {
        using var doc = JsonDocument.Parse(responseText);
        var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : "";
        return status == "OK" ? null : status;
    }

    private async Task<IActionResult> Run(string service, Func<Task<HttpResponseMessage>> call, Func<string, string?> findError)
    {
        if (!GoogleGateway.IsConfigured)
            return Ok(new { status = "not_configured", message = "Google APIs are not connected yet.", service });
        try
        {
            using var response = await call();
            var responseText = await response.Content.ReadAsStringAsync();
            if ((int)response.StatusCode is 402 or 429)
                return Ok(new { status = "paused", message = PausedMessage(responseText), service });
            if (!response.IsSuccessStatusCode)
                return Ok(new { status = "error", message = responseText.Length > 200 ? responseText.Substring(0, 200) + "..." : responseText, service });
            var error = findError(responseText);
            return error == null
                ? Ok(new { status = "ok", message = $"{service} responded successfully.", service })
                : Ok(new { status = "error", message = error, service });
        }
        catch (Exception ex)
        {
            return Ok(new { status = "error", message = ex.Message, service });
        }
    }

    private static string PausedMessage(string responseText)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseText);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String) return e.GetString()!;
        }
        catch (JsonException)
        {
        }
        return "Google features are paused for this project.";
    }
}