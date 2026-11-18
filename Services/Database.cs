using Npgsql;

namespace Backend.Services;

/// <summary>Opens database connections with the same search_path the other controllers use.</summary>
public static class Database
{
    // The database role has a restricted default search_path, so public is set first
    public static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var setPath = new NpgsqlCommand("SET search_path = public, \"$user\";", conn);
        await setPath.ExecuteNonQueryAsync();
        return conn;
    }
}
