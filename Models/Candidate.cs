namespace Backend.Models;

/// <summary>One made-up candidate in the candidates table.</summary>
public class Candidate
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Headline { get; set; } = "";
    public string City { get; set; } = "";
    public string Email { get; set; } = "";
}
