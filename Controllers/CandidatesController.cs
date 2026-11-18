using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>Reads the made-up candidates (CV-style sample data).</summary>
[ApiController]
[Route("api/candidates")]
public class CandidatesController : ControllerBase
{
    private readonly string _connectionString;

    public CandidatesController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not found");
    }

    /// <summary>GET /api/candidates - every made-up candidate.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Candidate>>> GetAll()
    {
        return Ok(await CandidateStore.GetAllAsync(_connectionString));
    }
}
