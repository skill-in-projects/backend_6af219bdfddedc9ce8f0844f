using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Lists the bike shop's repair bookings for today.</summary>
[ApiController]
[Route("api/bike-shop/repair-bookings")]
public class RepairBookingsController : ControllerBase
{
    private readonly string _connectionString;

    public RepairBookingsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    /// <summary>GET /api/bike-shop/repair-bookings/today - today's repair bookings, earliest first.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetToday()
    {
        var bookings = await RepairBookingStore.GetTodayAsync(_connectionString);
        return Ok(new
        {
            status = "ok",
            bookings = bookings.Select(b => new { id = b.Id, customerName = b.CustomerName, bike = b.Bike, service = b.Service, time = b.Time })
        });
    }
}
