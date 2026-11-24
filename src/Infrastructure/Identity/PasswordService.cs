using Microsoft.AspNetCore.Identity;
using Application.Auth;
using Domain.Entities;

namespace Infrastructure.Identity;

/// <summary>ASP.NET Core Identity password hashing (PBKDF2); the hasher does not read the user, so a blank User is enough.</summary>
public sealed class PasswordService(IPasswordHasher<User> hasher) : IPasswordService
{
    private static readonly User Anyone = new();

    public string Hash(string password) => hasher.HashPassword(Anyone, password);

    // No look-alike characters (0/O, 1/l/I) so the password can be read from the email and typed.
    public string NewTemporaryPassword() =>
        System.Security.Cryptography.RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);

    public bool Verify(string storedHash, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(Anyone, storedHash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
