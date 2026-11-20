using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Application.Persistence;

namespace Tests;

/// <summary>File storage kept in memory for tests (same contract as Cloudinary / local disk, no conversion).</summary>
public sealed class TestFileStorage : IFileStorage
{
    public readonly Dictionary<string, byte[]> Files = new();
    public readonly Dictionary<string, DateTime> Created = new();

    public bool ConvertsImages { get; init; }

    public Task<StoredObject> UploadAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        // Like Cloudinary: phone photos come back as JPEG.
        if (ConvertsImages && contentType is "image/heic" or "image/webp" or "image/avif") (key, contentType) = (key[..^Path.GetExtension(key).Length] + ".jpg", "image/jpeg");
        Files[key] = ms.ToArray();
        Created[key] = DateTime.UtcNow;
        return Task.FromResult(new StoredObject(key, contentType, ms.Length));
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Files.Remove(key);
        Created.Remove(key);
        return Task.CompletedTask;
    }

    public Task<string?> GetTemporaryUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public async IAsyncEnumerable<StorageItem> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (key, created) in Created.ToList()) yield return new StorageItem(key, created);
        await Task.CompletedTask;
    }
}
