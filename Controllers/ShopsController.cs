using System.Collections.Concurrent;
using System.Text.Json;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Works out how long it takes to walk from one address to another. Each new lookup uses the project's credits.</summary>
[ApiController]
[Route("api/shops")]
public class ShopsController : ControllerBase
{
    // Walking times already looked up, so repeating a lookup does not use credits again.
    private static readonly ConcurrentDictionary<string, int> WalkMinutes = new();

    /// <summary>GET /api/shops/walk-time?from=...&amp;to=... - Walking time in minutes between two addresses.</summary>
    [HttpGet("walk-time")]
    public async Task<IActionResult> WalkTime([FromQuery] string? from, [FromQuery] string? to)
    {
        var origin = (from ?? "").Trim();
        var destination = (to ?? "").Trim();
        if (origin.Length == 0 || destination.Length == 0) return Ok(new { status = "error", message = "Please type both addresses." });
        var key = $"{origin}|{destination}".ToLowerInvariant();
        if (WalkMinutes.TryGetValue(key, out var known)) return Ok(new { status = "ok", minutes = known });
        if (!GoogleGateway.IsConfigured) return Ok(new { status = "not_configured", message = "Walking times are not available in this app yet." });

        try
        {
            var path = $"/maps/api/directions/json?origin={Uri.EscapeDataString(origin)}&destination={Uri.EscapeDataString(destination)}&mode=walking";
            using var response = await GoogleGateway.GetAsync("maps", path);
            var text = await response.Content.ReadAsStringAsync();

            if ((int)response.StatusCode is 402 or 429)
            {
                var code = ReadCode(text);
                if (code == "out_of_credits") return Ok(new { status = "paused", message = "Walking times are paused because the project has run out of credits for now." });
                if (code is "daily_credit_limit" or "daily_call_limit" or "run_call_limit") return Ok(new { status = "paused", message = "Walking times have reached today's limit. Please try again tomorrow." });
                return Ok(new { status = "unavailable", message = "Walking times are unavailable right now. Please try again later." });
            }
            if (!response.IsSuccessStatusCode) return Ok(new { status = "error", message = "Could not get the walking time right now. Please try again." });

            using var doc = JsonDocument.Parse(text);
            var googleStatus = doc.RootElement.GetProperty("status").GetString();
            if (googleStatus is "ZERO_RESULTS" or "NOT_FOUND") return Ok(new { status = "not_found", message = "Could not find a walking route. Try adding a city or country." });
            if (googleStatus == "OVER_QUERY_LIMIT") return Ok(new { status = "paused", message = "Walking times are paused for now. Please try again later." });
            if (googleStatus != "OK") return Ok(new { status = "error", message = "Could not get the walking time right now. Please try again." });

            var seconds = doc.RootElement.GetProperty("routes")[0].GetProperty("legs")[0].GetProperty("duration").GetProperty("value").GetDouble();
            var minutes = Math.Max(1, (int)Math.Round(seconds / 60));
            WalkMinutes[key] = minutes;
            return Ok(new { status = "ok", minutes });
        }
        catch (Exception)
        {
            return Ok(new { status = "error", message = "Could not get the walking time right now. Please try again." });
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
