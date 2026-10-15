namespace RaceDay.Api.Security;

/// <summary>Passwords are never stored in plain text: only a salted BCrypt hash is saved.</summary>
public static class PasswordService
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false; // malformed hash (e.g. placeholder seed data)
        }
    }
}
