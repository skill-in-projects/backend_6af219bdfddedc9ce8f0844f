namespace Backend.Models;

/// <summary>One row of the truck_drivers table.</summary>
public class TruckDriver
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string LicenseClass { get; set; } = "";
    public int YearsDriving { get; set; }
}
