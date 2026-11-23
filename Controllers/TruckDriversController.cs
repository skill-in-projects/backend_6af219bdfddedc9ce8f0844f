using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Reads the truck_drivers table (sample data for now).</summary>
[ApiController]
[Route("api/truck-drivers")]
public class TruckDriversController : ControllerBase
{
    private readonly string _connectionString;

    public TruckDriversController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    /// <summary>GET /api/truck-drivers - every truck driver in the table.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TruckDriver>>> GetAll()
    {
        return Ok(await TruckDriverStore.GetAllAsync(_connectionString));
    }
}
