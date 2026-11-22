using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Newsletter sign-up for bike shop customers. Saves the email address only; nothing is sent from here.</summary>
[ApiController]
[Route("api/bike-shop/newsletter")]
public class NewsletterController : ControllerBase
{
    private const int MaxEmailLength = 254;

    private readonly string _connectionString;

    public NewsletterController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    public class SignUpRequest
    {
        public string Email { get; set; } = "";
    }

    /// <summary>POST /api/bike-shop/newsletter - adds the email to the newsletter list.</summary>
    [HttpPost]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || email.Length > MaxEmailLength || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            return Ok(new { status = "error", message = "Please type a valid email address." });

        var added = await NewsletterStore.AddAsync(_connectionString, email);
        return Ok(new
        {
            status = "ok",
            message = added ? "Thanks! You are on the list." : "You are already on the list."
        });
    }
}
