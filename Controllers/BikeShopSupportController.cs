using System.Text.Json;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>A support agent for a bike shop: answers questions about repairs and opening hours. Each message uses the project's credits. Conversations are saved per customer, or per account when the customer is signed in.</summary>
[ApiController]
[Route("api/bike-shop/chat")]
public class BikeShopSupportController : ControllerBase
{
    private const string GeminiPath = "/v1beta/models/gemini-3.5-flash-lite:generateContent";
    private const int MaxMessageLength = 1000;
    private const int MaxCustomerIdLength = 64;
    private const int MaxHistory = 10;
    private const int MaxSavedMessages = 50;
    private const string AccountPrefix = "account:";
    private static readonly string SystemPrompt =
        "You are the customer support agent of a bike shop. Answer customers' questions about repairs and opening hours " +
        "in a friendly, short way, in plain words. Answer only from the shop information below. If the answer is not " +
        "in that information, say you are not sure and suggest the customer visit the shop during opening hours. " +
        "Never invent prices, times or policies.\n\nShop information:\n" + BikeShopInfo.Text;

    private readonly string _connectionString;

    public BikeShopSupportController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    public class ChatRequest
    {
        public string CustomerId { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>GET /api/bike-shop/chat?customerId=... - the customer's saved conversation, oldest first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory([FromQuery] string customerId)
    {
        var (key, signedOut) = ResolveCustomer(customerId);
        if (signedOut) return Ok(new { status = "signed_out", message = "Please sign in again." });
        if (key == null) return Ok(new { status = "error", message = "Missing customer." });
        var messages = await BikeShopStore.GetRecentAsync(_connectionString, key, MaxSavedMessages);
        return Ok(new { status = "ok", messages = messages.Select(m => new { role = m.Role, text = m.Text }) });
    }

    /// <summary>DELETE /api/bike-shop/chat?customerId=... - clears the customer's saved conversation.</summary>
    [HttpDelete]
    public async Task<IActionResult> ClearHistory([FromQuery] string customerId)
    {
        var (key, signedOut) = ResolveCustomer(customerId);
        if (signedOut) return Ok(new { status = "signed_out", message = "Please sign in again." });
        if (key == null) return Ok(new { status = "error", message = "Missing customer." });
        await BikeShopStore.ClearAsync(_connectionString, key);
        return Ok(new { status = "ok" });
    }

    /// <summary>POST /api/bike-shop/chat - One reply from Gemini, given the new question and the customer's saved earlier turns.</summary>
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ChatRequest request)
    {
        var (key, signedOut) = ResolveCustomer(request.CustomerId);
        if (signedOut) return Ok(new { status = "signed_out", message = "Please sign in again." });
        if (key == null) return Ok(new { status = "error", message = "Missing customer." });
        var question = (request.Message ?? "").Trim();
        if (question.Length == 0) return Ok(new { status = "error", message = "Please type a question first." });
        if (question.Length > MaxMessageLength) return Ok(new { status = "error", message = "That message is too long. Please shorten it." });
        if (!GoogleGateway.IsConfigured) return Ok(new { status = "not_configured", message = "The shop assistant is not available in this app yet." });

        // Only the last few saved turns are sent with the new question
        var saved = await BikeShopStore.GetRecentAsync(_connectionString, key, MaxHistory);
        var contents = saved
            .Select(turn => new { role = turn.Role, parts = new[] { new { text = turn.Text } } })
            .Append(new { role = "user", parts = new[] { new { text = question } } })
            .ToArray();

        string reply;
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
            reply = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim() ?? "";
        }
        catch (Exception)
        {
            return Ok(new { status = "error", message = "The shop assistant could not answer right now. Please try again." });
        }
        if (reply.Length == 0) return Ok(new { status = "error", message = "The shop assistant could not answer right now. Please try again." });

        // Saved only after a reply, so a failed question does not leave half a conversation
        await BikeShopStore.AddAsync(_connectionString, key, "user", question);
        await BikeShopStore.AddAsync(_connectionString, key, "model", reply);
        return Ok(new { status = "ok", reply });
    }

    /// <summary>
    /// Picks the key that saves the conversation. A valid sign-in token wins: the account's own key is used and the
    /// browser id is ignored. A token that is present but no longer valid returns signedOut. Without a token the
    /// browser id is used, and it may not borrow an account key.
    /// </summary>
    private (string? Key, bool SignedOut) ResolveCustomer(string? customerId)
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            var userId = JwtService.Validate(header["Bearer ".Length..]);
            return userId == null ? (null, true) : ($"{AccountPrefix}{userId}", false);
        }

        var id = (customerId ?? "").Trim();
        if (!IsValidCustomerId(id) || id.StartsWith(AccountPrefix, StringComparison.Ordinal)) return (null, false);
        return (id, false);
    }

    private static bool IsValidCustomerId(string? customerId) =>
        !string.IsNullOrWhiteSpace(customerId) && customerId.Length <= MaxCustomerIdLength;

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
