using System.Runtime.CompilerServices;
using Application.Persistence;

namespace Infrastructure.Files;

public sealed class FileStorageOptions
{
    public required string RootPath { get; init; }
    public required string SigningKey { get; init; }
    public TimeSpan UrlLifetime { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>Development fallback when Cloudinary is not configured: files under Files:RootPath, served by the API.</summary>
public sealed class LocalDiskFileStorage(FileStorageOptions options) : IFileStorage
{
    // The disk keeps bytes as they are: HEIC/WebP photos cannot be converted here and are refused by FileService.
    public bool ConvertsImages => false;

    public async Task<StoredObject> UploadAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, cancellationToken);
        return new StoredObject(key, contentType, file.Length);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolve(key));
        return Task.CompletedTask;
    }

    public Task<string?> GetTemporaryUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public async IAsyncEnumerable<StorageItem> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(options.RootPath);
        if (!Directory.Exists(root)) yield break;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new StorageItem(Path.GetRelativePath(root, path).Replace('\\', '/'), File.GetCreationTimeUtc(path));
        }
        await Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var root = Path.GetFullPath(options.RootPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Khóa lưu trữ không hợp lệ.");
        return path;
    }
}
