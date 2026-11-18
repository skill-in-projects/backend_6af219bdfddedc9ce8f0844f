namespace Backend.Models;

/// <summary>One row of the cowboys table.</summary>
public class Cowboy
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int BeltSize { get; set; }
    public decimal Salary { get; set; }
}
