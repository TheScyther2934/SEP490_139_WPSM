namespace Application.Auth;

/// <summary>Hashes and checks account passwords (ASP.NET Core Identity format), so callers never need the User entity.</summary>
public interface IPasswordService
{
    string Hash(string password);
    /// <summary>False for a wrong password or an unreadable stored hash.</summary>
    bool Verify(string storedHash, string password);
    /// <summary>A random 16-character password for a new or reset customer account (sent by email, changed at first sign-in).</summary>
    string NewTemporaryPassword();
}
