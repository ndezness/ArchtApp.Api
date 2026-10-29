using System.Text.RegularExpressions;

namespace ArchtApp.Api.Services;

public static class UserRules
{
    public static readonly string[] Roles = { "admin", "staff" };

    private static readonly Regex UsernamePattern = new("^[a-z0-9._-]{3,32}$");

    // Usernames are stored lowercase so "Sam" and "sam" can't both exist
    public static string Normalize(string? username) =>
        (username ?? "").Trim().ToLowerInvariant();

    public static string? ValidateUsername(string normalized) =>
        UsernamePattern.IsMatch(normalized)
            ? null
            : "Username must be 3-32 characters: letters, numbers, dot, dash or underscore.";

    // BCrypt only uses the first 72 characters, so cap it there
    public static string? ValidatePassword(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 72
            ? "Password must be 8-72 characters."
            : null;
}