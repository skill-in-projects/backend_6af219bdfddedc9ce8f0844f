using Backend.Models;
using Npgsql;

namespace Backend.Services;

/// <summary>Creates the app_users table and saves and finds sign-up accounts.</summary>
public static class UserStore
{
    /// <summary>Creates the users table if it is missing.</summary>
    public static async Task EnsureTableAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS app_users (
                id serial PRIMARY KEY,
                email text NOT NULL UNIQUE,
                password_hash text NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Saves a new user. Returns the new id, or null when the email is already registered.</summary>
    public static async Task<int?> CreateAsync(string connectionString, string email, string passwordHash)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO app_users (email, password_hash) VALUES (@email, @hash) ON CONFLICT (email) DO NOTHING RETURNING id;", conn);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        var result = await cmd.ExecuteScalarAsync();
        return result is int id ? id : null;
    }

    /// <summary>Finds a user by email, or returns null.</summary>
    public static async Task<AppUser?> FindByEmailAsync(string connectionString, string email)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("SELECT id, email, password_hash FROM app_users WHERE email = @email;", conn);
        cmd.Parameters.AddWithValue("email", email);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new AppUser(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
    }
}
