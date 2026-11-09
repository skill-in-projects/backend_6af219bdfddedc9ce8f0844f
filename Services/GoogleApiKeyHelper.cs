namespace Backend.Services;

/// <summary>
/// Helper to read Google API keys from environment.
/// GOOGLE_API_KEY is for Gemini only; GOOGLE_MAPS_API_KEY is for Geocoding, Maps, Directions, Places, and Speech-to-Text
/// (Google does not allow those APIs on the same key as Gemini).
/// </summary>
public static class GoogleApiKeyHelper
{
    public static string? GetGoogleApiKey() => Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

    public static string? GetMapsApiKey()
    {
        var key = Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? GetGoogleApiKey() : key;
    }
}
