using System.Text.Json;
using Npgsql;

namespace Backend.Middleware;

/// <summary>
/// Skill-in solo workspace (platform file). In the free trial the database is read-only: a refused write
/// (Postgres 42501, insufficient privilege) answers 403 { code: "saving_data_needs_paid_workspace" }, which the
/// frontend turns into "Saving data needs a paid workspace" with an Upgrade button.
/// </summary>
public class ReadOnlyDatabaseMiddleware
{
    private readonly RequestDelegate _next;

    public ReadOnlyDatabaseMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (IsReadOnlyRefusal(ex) && !context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "saving_data_needs_paid_workspace",
                message = "Saving data needs a paid workspace."
            }));
        }
    }

    private static bool IsReadOnlyRefusal(Exception? ex)
    {
        for (; ex != null; ex = ex.InnerException)
            if (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.InsufficientPrivilege) return true;
        return false;
    }
}
