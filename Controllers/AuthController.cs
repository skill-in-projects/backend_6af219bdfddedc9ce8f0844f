using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Sign up, sign in and a check of who is signed in, using JWT tokens.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const int MinPasswordLength = 8;
    private readonly string _connectionString;

    public class Credentials
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public AuthController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    /// <summary>POST /api/auth/register - Creates an account and returns a sign-in token.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Credentials body)
    {
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        if (!email.Contains('@') || email.Length > 254) return Ok(new { status = "error", message = "Please enter a valid email address." });
        var password = body.Password ?? "";
        if (password.Length < MinPasswordLength) return Ok(new { status = "error", message = "The password needs at least 8 characters." });

        var userId = await UserStore.CreateAsync(_connectionString, email, PasswordHasher.Hash(password));
        if (userId == null) return Ok(new { status = "error", message = "That email is already registered." });
        return Ok(new { status = "ok", token = JwtService.Create(userId.Value, email) });
    }

    /// <summary>POST /api/auth/login - Checks the email and password and returns a sign-in token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Credentials body)
    {
        var email = (body.Email ?? "").Trim().ToLowerInvariant();
        var user = await UserStore.FindByEmailAsync(_connectionString, email);
        if (user == null || !PasswordHasher.Verify(body.Password ?? "", user.PasswordHash))
        {
            return Ok(new { status = "error", message = "Email or password is wrong." });
        }
        return Ok(new { status = "ok", token = JwtService.Create(user.Id, user.Email) });
    }

    /// <summary>GET /api/auth/me - Says who is signed in, based on the token sent in the Authorization header.</summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ") ? header["Bearer ".Length..] : null;
        var userId = JwtService.Validate(token);
        if (userId == null) return Ok(new { status = "signed_out" });
        return Ok(new { status = "ok", userId });
    }
}
