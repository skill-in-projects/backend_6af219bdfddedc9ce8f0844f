using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// Makes and checks JWT sign-in tokens (HS256). The signing key is random and made when the backend starts,
/// so every token stops working after a restart and users sign in again.
/// </summary>
public static class JwtService
{
    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    public static string Create(int userId, string email)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            sub = userId,
            email,
            exp = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds()
        })));
        return $"{header}.{payload}.{Sign($"{header}.{payload}")}";
    }

    /// <summary>Returns the user id inside a valid, unexpired token, or null when the token is missing, altered or expired.</summary>
    public static int? Validate(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        var expected = Encoding.UTF8.GetBytes(Sign($"{parts[0]}.{parts[1]}"));
        if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.UTF8.GetBytes(parts[2]))) return null;

        try
        {
            using var doc = JsonDocument.Parse(FromBase64Url(parts[1]));
            var exp = doc.RootElement.GetProperty("exp").GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp) return null;
            return doc.RootElement.GetProperty("sub").GetInt32();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Sign(string text) => Base64Url(HMACSHA256.HashData(SigningKey, Encoding.UTF8.GetBytes(text)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        return Convert.FromBase64String(base64);
    }
}
