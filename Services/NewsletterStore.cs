using Npgsql;

namespace Backend.Services;

/// <summary>Creates the newsletter_signups table and saves each email address once.</summary>
public static class NewsletterStore
{
    /// <summary>Creates the sign-ups table if it is missing.</summary>
    public static async Task EnsureTableAsync(string connectionString)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS newsletter_signups (
                id serial PRIMARY KEY,
                email text NOT NULL UNIQUE,
                created_at timestamptz NOT NULL DEFAULT now()
            );", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Saves the email. Returns false when it was already on the list.</summary>
    public static async Task<bool> AddAsync(string connectionString, string email)
    {
        await using var conn = await Database.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO newsletter_signups (email) VALUES (@email) ON CONFLICT (email) DO NOTHING;", conn);
        cmd.Parameters.AddWithValue("email", email);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
}
