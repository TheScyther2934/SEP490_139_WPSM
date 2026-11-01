namespace WPSM.Application.Persistence;

public sealed record FileUploadCommand(Stream Content, string OriginalName, string Purpose, string ProjectCode, int UploadedBy);
public sealed record StoredFileView(Guid Id, string Purpose, string OriginalName, string ContentType, long SizeBytes, DateTime UploadedAt);
public sealed record SignedFileUrl(string Url, DateTime ExpiresAt);
public sealed record FileContent(Stream Content, string ContentType, string OriginalName);

public interface IFileStorage
{
    Task UploadAsync(Stream content, string key, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public interface IFileService
{
    /// <summary>API-01. Returns null when the project does not exist or is not assigned to the uploader (GBR-12).</summary>
    Task<StoredFileView?> UploadAsync(FileUploadCommand command, CancellationToken cancellationToken = default);
    /// <summary>API-02. Returns null when the user may not access the file (GBR-12).</summary>
    Task<SignedFileUrl?> GetSignedUrlAsync(Guid fileId, int userId, string? role, CancellationToken cancellationToken = default);
    Task<FileContent?> OpenSignedAsync(Guid fileId, long expires, string signature, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);
}
