using WPSM.Application.Persistence;

namespace WPSM.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public required string RootPath { get; init; }
    public required string SigningKey { get; init; }
    public TimeSpan UrlLifetime { get; init; } = TimeSpan.FromMinutes(5);
}

public sealed class LocalDiskFileStorage(FileStorageOptions options) : IFileStorage
{
    public async Task UploadAsync(Stream content, string key, CancellationToken cancellationToken = default)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, cancellationToken);
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

    private string Resolve(string key)
    {
        var root = Path.GetFullPath(options.RootPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Khóa lưu trữ không hợp lệ.");
        return path;
    }
}
