namespace Application.Persistence;

public sealed record FileUploadCommand(Stream Content, string OriginalName, string Purpose, string ProjectCode, int UploadedBy);
/// <summary>A stored file. StorageKey is where the bytes live (never a URL): tables keep FileId/StorageKey, URLs are made when needed.</summary>
public sealed record StoredFileView(Guid Id, string Purpose, string OriginalName, string ContentType, long SizeBytes, DateTime UploadedAt)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string StorageKey { get; init; } = "";
}
public sealed record SignedFileUrl(string Url, DateTime ExpiresAt);
/// <summary>A stored file to serve: its storage key, type and the name shown to the user.</summary>
public sealed record StoredFileRef(string StorageKey, string ContentType, string FileName);
/// <summary>Either the bytes (Content) or a short-lived address to send the browser to (RedirectUrl, cloud storage images).</summary>
public sealed record FileContent(Stream? Content, string ContentType, string OriginalName, string? RedirectUrl = null);

/// <summary>What the storage kept: the final key and type (an iPhone HEIC photo is kept as JPEG).</summary>
public sealed record StoredObject(string Key, string ContentType, long SizeBytes);

/// <summary>An object found in the storage (orphan cleanup).</summary>
public sealed record StorageItem(string Key, DateTime CreatedAt);

/// <summary>
/// Where file bytes live: Cloudinary (private assets) or the local disk (development without Cloudinary).
/// Keys are relative ("{event}/{purpose}/{fileId}.ext"); the storage adds its own root/folder (environment).
/// </summary>
public interface IFileStorage
{
    /// <summary>True when the storage converts HEIC/HEIF/WebP photos to JPEG and resizes large photos (Cloudinary).</summary>
    bool ConvertsImages { get; }
    Task<StoredObject> UploadAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>A short-lived address the browser can load directly, or null when the file must be streamed by the API.</summary>
    Task<string?> GetTemporaryUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StorageItem> ListAsync(CancellationToken cancellationToken = default);
}

public interface IFileService
{
    /// <summary>API-01. Returns null when the project does not exist or is not assigned to the uploader (GBR-12).</summary>
    Task<StoredFileView?> UploadAsync(FileUploadCommand command, CancellationToken cancellationToken = default);
    /// <summary>API-02. Returns null when the user may not access the file (GBR-12).</summary>
    Task<SignedFileUrl?> GetSignedUrlAsync(Guid fileId, int userId, string? role, CancellationToken cancellationToken = default);
    Task<FileContent?> OpenSignedAsync(Guid fileId, long expires, string signature, CancellationToken cancellationToken = default);
    /// <summary>
    /// The one way to store a file (uploads and generated PDFs): checks type by signature (JPG, PNG, PDF, and HEIC/HEIF/WebP photos
    /// converted to JPEG), size (10 MB), strips photo metadata, names it "{event}/{purpose}/{fileId}" and records it in StoredFiles.
    /// </summary>
    Task<StoredFileView> StoreAsync(int projectId, string purpose, string originalName, byte[] content, int? uploadedBy, CancellationToken cancellationToken = default);
    /// <summary>Serves a stored file by key: images from cloud storage as a short-lived redirect, everything else as bytes.</summary>
    Task<FileContent?> OpenAsync(string storageKey, string contentType, string fileName, CancellationToken cancellationToken = default);
    /// <summary>The bytes of a stored file (receipts and documents the API returns itself); null when missing.</summary>
    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
}
