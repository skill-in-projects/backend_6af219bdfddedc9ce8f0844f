using Npgsql;

namespace Backend.Services;

/// <summary>Creates the repair_bookings table, adds sample bookings for today once, and reads today's bookings.</summary>
public static class RepairBookingStore
{
    /// <summary>Creates the table if it is missing, and adds sample bookings for today if the table is empty.</summary>
    public static async Task EnsureTableAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);

        await using (var create = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS repair_bookings (
                id serial PRIMARY KEY,
                customer_name text NOT NULL,
                bike text NOT NULL,
                service text NOT NULL,
                booking_date date NOT NULL,
                booking_time time NOT NULL
            );", conn))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM repair_bookings;", conn))
        {
            if (Convert.ToInt64(await count.ExecuteScalarAsync()) > 0) return;
        }

        // Sample rows use today's date so the "today" list has something to show
        string[] samples =
        {
            "INSERT INTO repair_bookings (customer_name, bike, service, booking_date, booking_time) VALUES ('Dana Reyes', 'Trek hybrid', 'Brake adjustment', CURRENT_DATE, '09:00');",
            "INSERT INTO repair_bookings (customer_name, bike, service, booking_date, booking_time) VALUES ('Sam Keller', 'Road bike', 'Puncture repair', CURRENT_DATE, '11:30');",
            "INSERT INTO repair_bookings (customer_name, bike, service, booking_date, booking_time) VALUES ('Lee Walsh', 'Mountain bike', 'Gear tune-up', CURRENT_DATE, '14:00');"
        };
        foreach (var sql in samples)
        {
            await using var insert = new NpgsqlCommand(sql, conn);
            await insert.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Returns today's repair bookings, earliest first.</summary>
    public static async Task<List<(int Id, string CustomerName, string Bike, string Service, string Time)>> GetTodayAsync(string connectionString)
    {
        var bookings = new List<(int Id, string CustomerName, string Bike, string Service, string Time)>();
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, customer_name, bike, service, to_char(booking_time, 'HH24:MI')
            FROM repair_bookings
            WHERE booking_date = CURRENT_DATE
            ORDER BY booking_time, id;", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            bookings.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return bookings;
    }
}
