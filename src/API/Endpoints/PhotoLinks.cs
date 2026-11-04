using System.Security.Cryptography;
using System.Text;
using Infrastructure.Files;

namespace API.Endpoints;

// <img> tags cannot send a Bearer header, so proof photos are reached through short-lived signed URLs (SDS III.1.3).
// Expiry is rounded to 5-minute windows so a polling page keeps the same URL and the browser cache stays useful.
public sealed class PhotoLinks(FileStorageOptions options)
{
    private const long Window = 300;

    public string Url(int photoId)
    {
        var expires = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / Window + 2) * Window;
        return $"/api/wpsm/photos/{photoId}/content?expires={expires}&sig={Sign(photoId, expires)}";
    }

    public bool Verify(int photoId, long expires, string signature) =>
        expires >= DateTimeOffset.UtcNow.ToUnixTimeSeconds() &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(photoId, expires)), Encoding.ASCII.GetBytes(signature ?? ""));

    private string Sign(int photoId, long expires) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), Encoding.UTF8.GetBytes($"photo:{photoId}:{expires}")));
}
