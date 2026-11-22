using Npgsql;

namespace Backend.Services;

/// <summary>Creates the bike_shop_messages table and saves, reads and clears each customer's conversation.</summary>
public static class BikeShopStore
{
    /// <summary>Creates the messages table if it is missing.</summary>
    public static async Task EnsureTableAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS bike_shop_messages (
                id serial PRIMARY KEY,
                customer_id text NOT NULL,
                role text NOT NULL,
                text text NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS bike_shop_messages_customer_idx ON bike_shop_messages (customer_id, id);", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Saves one message (role is "user" or "model") for a customer.</summary>
    public static async Task AddAsync(string connectionString, string customerId, string role, string text)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO bike_shop_messages (customer_id, role, text) VALUES (@customer, @role, @text);", conn);
        cmd.Parameters.AddWithValue("customer", customerId);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("text", text);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Returns the customer's latest messages, oldest first.</summary>
    public static async Task<List<(string Role, string Text)>> GetRecentAsync(string connectionString, string customerId, int limit)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(@"
            SELECT role, text FROM (
                SELECT id, role, text FROM bike_shop_messages WHERE customer_id = @customer ORDER BY id DESC LIMIT @limit
            ) recent ORDER BY id;", conn);
        cmd.Parameters.AddWithValue("customer", customerId);
        cmd.Parameters.AddWithValue("limit", limit);
        await using var reader = await cmd.ExecuteReaderAsync();
        var messages = new List<(string Role, string Text)>();
        while (await reader.ReadAsync())
            messages.Add((reader.GetString(0), reader.GetString(1)));
        return messages;
    }

    /// <summary>Deletes the customer's whole conversation.</summary>
    public static async Task ClearAsync(string connectionString, string customerId)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("DELETE FROM bike_shop_messages WHERE customer_id = @customer;", conn);
        cmd.Parameters.AddWithValue("customer", customerId);
        await cmd.ExecuteNonQueryAsync();
    }
}
