using System.Text;

namespace Backend.Services;

/// <summary>
/// Skill-in platform file. The one door for every Google call (Gemini, Geocoding, Directions, Places, Speech-to-Text).
/// This app has no Google keys and cannot reach Google directly. Each call uses the project's credits.
/// Service is "gemini", "maps", "places" or "speech"; pathAndQuery is Google's own path, starting with "/".
/// When the project is out of credits the answer is 402, at its daily limit 429.
/// </summary>
public static class GoogleGateway
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private static string BaseUrl => (Environment.GetEnvironmentVariable("GOOGLE_PROXY_BASE_URL") ?? "").Trim().TrimEnd('/');
    private static string Token => (Environment.GetEnvironmentVariable("GOOGLE_PROXY_TOKEN") ?? "").Trim();

    public static bool IsConfigured => BaseUrl.Length > 0 && Token.Length > 0;

    public static Task<HttpResponseMessage> GetAsync(string service, string pathAndQuery) =>
        SendAsync(HttpMethod.Get, service, pathAndQuery, null, null);

    /// <summary>fieldMask is for Places: the X-Goog-FieldMask header.</summary>
    public static Task<HttpResponseMessage> PostJsonAsync(string service, string pathAndQuery, string json, string? fieldMask = null) =>
        SendAsync(HttpMethod.Post, service, pathAndQuery, json, fieldMask);

    private static Task<HttpResponseMessage> SendAsync(HttpMethod method, string service, string pathAndQuery, string? json, string? fieldMask)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl}/{service}{pathAndQuery}");
        request.Headers.TryAddWithoutValidation("X-Proxy-Token", Token);
        if (fieldMask != null) request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", fieldMask);
        if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return Http.SendAsync(request);
    }
}