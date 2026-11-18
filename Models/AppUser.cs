namespace Backend.Models;

/// <summary>A signed-up user, as stored in the app_users table. The password is kept only as a hash.</summary>
public record AppUser(int Id, string Email, string PasswordHash, string CowboyName);
