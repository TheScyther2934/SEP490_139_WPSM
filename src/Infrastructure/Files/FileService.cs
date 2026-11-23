using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Files;

/// <summary>
/// File rules (one place for every upload and generated PDF):
/// - the database keeps metadata (StoredFiles) and the storage key, never a URL and never the bytes;
/// - accepted: JPG, PNG, PDF, and phone photos HEIC/HEIF/WebP/AVIF (converted to JPEG by the storage), recognised by content, 10 MB at most;
/// - JPG/PNG metadata (GPS position, camera) is removed before storing;
/// - key = "{event code}/{purpose folder}/{file id}.{ext}": the id is a GUID, the user's file name stays in the database only;
/// - files are never deleted by the application (proof for complaints); orphans are removed by the weekly cleanup job.
/// </summary>
public sealed class FileService(WpsmDbContext db, IFileStorage storage, FileStorageOptions options) : IFileService
{
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Purposes allowed by CK_StoredFiles_Purpose, with their folder.</summary>
    private static readonly Dictionary<string, string> Folders = new()
    {
        ["ProofPhoto"] = "proof", ["Thumbnail"] = "proof",
        ["DepositReceipt"] = "receipts", ["PaymentReceipt"] = "receipts", ["SettlementReceipt"] = "receipts",
        ["SupplierEvidence"] = "suppliers", ["BeoSheet"] = "beo"
    };

    public async Task<StoredFileView?> UploadAsync(FileUploadCommand command, CancellationToken cancellationToken = default)
    {
        if (!Folders.ContainsKey(command.Purpose)) throw new InvalidOperationException("Loại tệp không hợp lệ.");
        var projectId = await db.WeddingProjects.AsNoTracking()
            .Where(x => x.ProjectCode == command.ProjectCode && (x.ManagerId == command.UploadedBy || x.ProjectManagers.Any(pm => pm.ManagerId == command.UploadedBy && pm.RemovedAt == null)))
            .Select(x => (int?)x.ProjectId).FirstOrDefaultAsync(cancellationToken);
        if (projectId is null) return null;

        using var buffer = new MemoryStream();
        await command.Content.CopyToAsync(buffer, cancellationToken);
        return await StoreAsync(projectId.Value, command.Purpose, command.OriginalName, buffer.ToArray(), command.UploadedBy, cancellationToken);
    }

    public async Task<StoredFileView> StoreAsync(int projectId, string purpose, string originalName, byte[] content, int? uploadedBy, CancellationToken cancellationToken = default)
    {
        if (!Folders.TryGetValue(purpose, out var folder)) throw new InvalidOperationException("Loại tệp không hợp lệ.");
        if (content.LongLength is 0 or > MaxBytes) throw new InvalidOperationException("Tệp phải có dung lượng từ 1 byte đến 10 MB (MSG17).");
        var type = FileTypes.Detect(content) ?? throw new InvalidOperationException(FileTypes.Refusal);
        if (type.NeedsConversion && !storage.ConvertsImages)
            throw new InvalidOperationException("Ảnh HEIC/WebP (iPhone, một số điện thoại Android) cần Cloudinary để chuyển sang JPG. Hãy cấu hình Cloudinary hoặc chọn ảnh JPG/PNG.");

        if (type.ContentType == "image/jpeg") content = ImageMetadataStripper.StripJpeg(content);
        else if (type.ContentType == "image/png") content = ImageMetadataStripper.StripPng(content);

        var projectCode = await db.WeddingProjects.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => x.ProjectCode).SingleAsync(cancellationToken);
        var fileId = Guid.NewGuid();
        var name = Path.GetFileName(originalName.Replace('\\', '/'));
        StoredObject stored;
        using (var stream = new MemoryStream(content, writable: false))
            stored = await storage.UploadAsync(stream, $"{projectCode}/{folder}/{fileId:N}{type.Extension}", type.ContentType, cancellationToken);
        // A converted photo keeps the user's name with the new extension (IMG_0012.HEIC -> IMG_0012.jpg).
        if (stored.ContentType != type.ContentType) name = Path.ChangeExtension(name, Path.GetExtension(stored.Key));

        var entity = new StoredFile
        {
            FileId = fileId,
            ProjectId = projectId,
            Purpose = purpose,
            StorageKey = stored.Key,
            OriginalName = name.Length > 200 ? name[^200..] : name,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow
        };
        try
        {
            db.StoredFiles.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(stored.Key, CancellationToken.None);
            throw;
        }
        return new StoredFileView(entity.FileId, entity.Purpose, entity.OriginalName, entity.ContentType, entity.SizeBytes, entity.UploadedAt) { StorageKey = entity.StorageKey };
    }

    public async Task<SignedFileUrl?> GetSignedUrlAsync(Guid fileId, int userId, string? role, CancellationToken cancellationToken = default)
    {
        var allowed = await db.StoredFiles.AsNoTracking().AnyAsync(x => x.FileId == fileId && x.Project != null &&
            ((role == "Customer" && x.Project.CustomerId == userId) || (role == "Manager" && (x.Project.ManagerId == userId || x.Project.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null)))), cancellationToken);
        if (!allowed) return null;
        var expiresAt = DateTimeOffset.UtcNow.Add(options.UrlLifetime);
        var expires = expiresAt.ToUnixTimeSeconds();
        return new SignedFileUrl($"/api/wpsm/files/{fileId}/content?expires={expires}&sig={Sign(fileId, expires)}", expiresAt.UtcDateTime);
    }

    public async Task<FileContent?> OpenSignedAsync(Guid fileId, long expires, string signature, CancellationToken cancellationToken = default)
    {
        if (expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(fileId, expires)), Encoding.ASCII.GetBytes(signature))) return null;
        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(x => x.FileId == fileId, cancellationToken);
        return file is null ? null : await OpenAsync(file.StorageKey, file.ContentType, file.OriginalName, cancellationToken);
    }

    public async Task<FileContent?> OpenAsync(string storageKey, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        // Photos go straight from the storage to the browser (link valid 5 minutes); PDFs and receipts pass through the API.
        if (contentType.StartsWith("image/", StringComparison.Ordinal) && await storage.GetTemporaryUrlAsync(storageKey, options.UrlLifetime, cancellationToken) is { } url)
            return new FileContent(null, contentType, fileName, url);
        var content = await storage.OpenReadAsync(storageKey, cancellationToken);
        return content is null ? null : new FileContent(content, contentType, fileName);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await using var content = await storage.OpenReadAsync(storageKey, cancellationToken);
        if (content is null) return null;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private string Sign(Guid fileId, long expires)
    {
        var payload = Encoding.UTF8.GetBytes($"{fileId:N}:{expires.ToString(CultureInfo.InvariantCulture)}");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), payload));
    }
}

/// <summary>File types recognised by their first bytes (the extension the user gives is not trusted).</summary>
public static class FileTypes
{
    public const string Refusal = "Chỉ nhận ảnh JPG, PNG, HEIC (iPhone), WebP hoặc tài liệu PDF hợp lệ (MSG17).";

    public sealed record Kind(string ContentType, string Extension, bool NeedsConversion);

    // ISO-BMFF brands of HEIF photos (iPhone "heic", "mif1"...) and AVIF.
    private static readonly HashSet<string> HeifBrands = ["heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1", "avif", "avis"];

    public static Kind? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return new("image/jpeg", ".jpg", false);
        if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return new("image/png", ".png", false);
        if (bytes.StartsWith("%PDF"u8)) return new("application/pdf", ".pdf", false);
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return new("image/webp", ".webp", true);
        if (bytes.Length >= 12 && bytes[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = Encoding.ASCII.GetString(bytes[8..12]);
            if (HeifBrands.Contains(brand)) return brand.StartsWith("avi") ? new("image/avif", ".avif", true) : new("image/heic", ".heic", true);
        }
        return null;
    }

    public static bool IsImage(ReadOnlySpan<byte> bytes) => Detect(bytes) is { } kind && kind.ContentType.StartsWith("image/", StringComparison.Ordinal);
}
