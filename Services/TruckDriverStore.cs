using Backend.Models;
using Npgsql;

namespace Backend.Services;

/// <summary>Creates the truck_drivers table, adds 30 sample rows once, and reads the rows back.</summary>
public static class TruckDriverStore
{
    private static readonly string[] FirstNames = { "Mike", "Anna", "Luis", "Priya", "Owen", "Grace", "Dmitri", "Sofia", "Kevin", "Hana" };
    private static readonly string[] LastNames = { "Brooks", "Novak", "Silva", "Patel", "Fischer", "Okafor", "Lindqvist", "Moreno", "Hayes", "Tanaka" };
    private static readonly string[] Streets = { "Highway Road", "Freight Lane", "Depot Street", "Harbor Drive", "Interstate Way" };
    private static readonly string[] Towns = { "Riverton", "Stonebridge", "Westfield", "Maple Ridge", "Eastport" };
    private static readonly string[] Licenses = { "Class A", "Class B", "Class C" };

    /// <summary>Creates the table if it is missing, and adds 30 sample rows if the table is empty.</summary>
    public static async Task EnsureSampleDataAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);

        await using (var create = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS truck_drivers (
                id serial PRIMARY KEY,
                name text NOT NULL,
                address text NOT NULL,
                license_class text NOT NULL,
                years_driving integer NOT NULL
            );", conn))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM truck_drivers;", conn))
        {
            if (Convert.ToInt64(await count.ExecuteScalarAsync()) > 0) return;
        }

        // A fixed seed means the sample rows are the same every time
        var random = new Random(43);
        await using var tx = await conn.BeginTransactionAsync();
        for (var i = 0; i < 30; i++)
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO truck_drivers (name, address, license_class, years_driving) VALUES (@name, @address, @license, @years);", conn, tx);
            insert.Parameters.AddWithValue("name", $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}");
            insert.Parameters.AddWithValue("address", $"{random.Next(10, 990)} {Streets[random.Next(Streets.Length)]}, {Towns[random.Next(Towns.Length)]}");
            insert.Parameters.AddWithValue("license", Licenses[random.Next(Licenses.Length)]);
            insert.Parameters.AddWithValue("years", random.Next(1, 35));
            await insert.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    /// <summary>Returns every truck driver, in the order they were added.</summary>
    public static async Task<List<TruckDriver>> GetAllAsync(string connectionString)
    {
        var drivers = new List<TruckDriver>();
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("SELECT id, name, address, license_class, years_driving FROM truck_drivers ORDER BY id;", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            drivers.Add(new TruckDriver
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Address = reader.GetString(2),
                LicenseClass = reader.GetString(3),
                YearsDriving = reader.GetInt32(4)
            });
        }
        return drivers;
    }
}
