using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Finds an address: its coordinates, and a map image with a marker on them.</summary>
[ApiController]
[Route("api/maps")]
public class MapsController : ControllerBase
{
    private record Place(double Lat, double Lng, string Address);

    // Addresses already looked up, so repeating a search does not use credits again.
    private static readonly ConcurrentDictionary<string, Place> Found = new();

    /// <summary>GET /api/maps/locate?address=... - Finds an address and returns its coordinates.</summary>
    [HttpGet("locate")]
    public async Task<IActionResult> Locate([FromQuery] string? address)
    {
        var query = (address ?? "").Trim();
        if (query.Length == 0) return Ok(new { status = "error", message = "Please type an address." });
        if (Found.TryGetValue(query.ToLowerInvariant(), out var known)) return Ok(new { status = "ok", known.Lat, known.Lng, address = known.Address });
        if (!GoogleGateway.IsConfigured) return Ok(new { status = "not_configured", message = "The map is not available in this app yet." });

        try
        {
            using var response = await GoogleGateway.GetAsync("maps", $"/maps/api/geocode/json?address={Uri.EscapeDataString(query)}");
            var text = await response.Content.ReadAsStringAsync();
            if ((int)response.StatusCode is 402 or 429) return Ok(new { status = "paused", message = "The map is paused for now. Please try again later." });
            if (!response.IsSuccessStatusCode) return Ok(new { status = "error", message = "The map service did not answer. Please try again." });

            using var doc = JsonDocument.Parse(text);
            var googleStatus = doc.RootElement.GetProperty("status").GetString();
            if (googleStatus == "ZERO_RESULTS") return Ok(new { status = "not_found", message = "Could not find that address. Try adding a city or country." });
            if (googleStatus == "OVER_QUERY_LIMIT") return Ok(new { status = "paused", message = "The map is paused for now. Please try again later." });
            if (googleStatus != "OK") return Ok(new { status = "error", message = "Could not look up that address. Please try again." });

            var first = doc.RootElement.GetProperty("results")[0];
            var location = first.GetProperty("geometry").GetProperty("location");
            var place = new Place(location.GetProperty("lat").GetDouble(), location.GetProperty("lng").GetDouble(), first.GetProperty("formatted_address").GetString() ?? query);
            Found[query.ToLowerInvariant()] = place;
            return Ok(new { status = "ok", place.Lat, place.Lng, address = place.Address });
        }
        catch (Exception ex)
        {
            return Ok(new { status = "error", message = ex.Message });
        }
    }

    /// <summary>GET /api/maps/image?lat=...&amp;lng=... - A map image (PNG) with a marker at the coordinates.</summary>
    [HttpGet("image")]
    public async Task<IActionResult> Image([FromQuery] double lat, [FromQuery] double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180) return BadRequest("Those coordinates are not on the map.");
        if (!GoogleGateway.IsConfigured) return StatusCode(503, "The map is not available in this app yet.");

        var center = $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";
        try
        {
            using var response = await GoogleGateway.GetAsync("maps", $"/maps/api/staticmap?center={center}&zoom=15&size=600x400&scale=2&markers=color:red%7C{center}");
            if ((int)response.StatusCode is 402 or 429) return StatusCode(503, "The map is paused for now. Please try again later.");
            if (!response.IsSuccessStatusCode) return StatusCode(502, "The map image could not be loaded right now.");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return File(bytes, "image/png");
        }
        catch (Exception)
        {
            return StatusCode(502, "The map image could not be loaded right now.");
        }
    }
}
