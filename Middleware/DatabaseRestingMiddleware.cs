using System.Text.Json;

namespace Backend.Middleware;

/// <summary>
/// When the database refuses because the project is over its monthly limit, answers 503 with code "database_resting",
/// so the frontend can show the calm "resting" page instead of an error.
/// </summary>
public class DatabaseRestingMiddleware
{
    private readonly RequestDelegate _next;

    public DatabaseRestingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (IsQuotaExceeded(ex) && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "database_resting",
                error = "This app is resting and will be back soon."
            }));
        }
    }

    private static bool IsQuotaExceeded(Exception? ex)
    {
        for (; ex != null; ex = ex.InnerException)
            if (ex.Message.Contains("exceeded", StringComparison.OrdinalIgnoreCase)
                && ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
