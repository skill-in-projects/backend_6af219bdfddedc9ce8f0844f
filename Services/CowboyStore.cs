using Backend.Models;
using Npgsql;

namespace Backend.Services;

/// <summary>Creates the cowboys table, adds 50 sample rows once, and reads the rows back.</summary>
public static class CowboyStore
{
    private static readonly string[] FirstNames = { "Jesse", "Clara", "Hank", "Maria", "Tom", "Lucy", "Ray", "Ellen", "Wade", "Rosa" };
    private static readonly string[] LastNames = { "Carter", "Hart", "Morales", "Dunn", "Keller", "Brooks", "Reyes", "Quinn", "Walsh", "Ortega" };
    private static readonly string[] Streets = { "Mesa Road", "Cedar Lane", "Sunset Trail", "Pine Street", "Ranch Way", "Canyon Drive", "Elm Avenue" };
    private static readonly string[] Towns = { "Dusty Creek", "Red Mesa", "Silver Bend", "Lone Pine", "Oak Flats" };

    /// <summary>Creates the table if it is missing, and adds 50 sample rows if the table is empty.</summary>
    public static async Task EnsureSampleDataAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);

        await using (var create = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS cowboys (
                id serial PRIMARY KEY,
                name text NOT NULL,
                address text NOT NULL,
                belt_size integer NOT NULL,
                salary numeric(10, 2) NOT NULL
            );", conn))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM cowboys;", conn))
        {
            if (Convert.ToInt64(await count.ExecuteScalarAsync()) > 0) return;
        }

        // A fixed seed means the sample rows are the same every time
        var random = new Random(42);
        await using var tx = await conn.BeginTransactionAsync();
        for (var i = 0; i < 50; i++)
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO cowboys (name, address, belt_size, salary) VALUES (@name, @address, @belt, @salary);", conn, tx);
            insert.Parameters.AddWithValue("name", $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}");
            insert.Parameters.AddWithValue("address", $"{random.Next(10, 990)} {Streets[random.Next(Streets.Length)]}, {Towns[random.Next(Towns.Length)]}");
            insert.Parameters.AddWithValue("belt", random.Next(30, 47));
            insert.Parameters.AddWithValue("salary", 30000m + random.Next(0, 61) * 1000m);
            await insert.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    /// <summary>Returns every cowboy, in the order they were added.</summary>
    public static async Task<List<Cowboy>> GetAllAsync(string connectionString)
    {
        var cowboys = new List<Cowboy>();
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("SELECT id, name, address, belt_size, salary FROM cowboys ORDER BY id;", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cowboys.Add(new Cowboy
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Address = reader.GetString(2),
                BeltSize = reader.GetInt32(3),
                Salary = reader.GetDecimal(4)
            });
        }
        return cowboys;
    }

    /// <summary>Raises one cowboy's salary by 10%. Returns the new salary, or null when no cowboy has that id.</summary>
    public static async Task<decimal?> RaiseSalaryAsync(string connectionString, int id)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("UPDATE cowboys SET salary = ROUND(salary * 1.10, 2) WHERE id = @id RETURNING salary;", conn);
        cmd.Parameters.AddWithValue("id", id);
        var result = await cmd.ExecuteScalarAsync();
        return result is decimal salary ? salary : null;
    }

}
