using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Reads the cowboys table (sample data for now).</summary>
[ApiController]
[Route("api/cowboys")]
public class CowboysController : ControllerBase
{
    private readonly string _connectionString;

    public CowboysController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    /// <summary>GET /api/cowboys - every cowboy in the table.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Cowboy>>> GetAll()
    {
        return Ok(await CowboyStore.GetAllAsync(_connectionString));
    }
}
