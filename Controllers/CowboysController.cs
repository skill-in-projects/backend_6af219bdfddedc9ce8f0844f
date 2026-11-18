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

    /// <summary>POST /api/cowboys/5/raise - gives one cowboy a 10% raise and returns the new salary.</summary>
    [HttpPost("{id}/raise")]
    public async Task<IActionResult> Raise(int id)
    {
        var salary = await CowboyStore.RaiseSalaryAsync(_connectionString, id);
        if (salary == null) return NotFound();
        return Ok(new { id, salary });
    }
}
