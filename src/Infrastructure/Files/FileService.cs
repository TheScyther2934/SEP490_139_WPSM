using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Files;

public sealed class FileService(WpsmDbContext db, IFileStorage storage, FileStorageOptions options) : IFileService
{
    private const long MaxBytes = 10 * 1024 * 1024;
    private static readonly string[] Purposes = ["NrdReceipt", "PaymentReceipt", "ProofPhoto", "Thumbnail", "AddendumPdf", "SignedAddendum", "BeoSheet", "SettlementReceipt"];
    private static readonly (byte[] Signature, string ContentType, string Extension)[] AllowedTypes =
    [
        ([0xFF, 0xD8, 0xFF], "image/jpeg", ".jpg"),
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "image/png", ".png"),
        ([0x25, 0x50, 0x44, 0x46], "application/pdf", ".pdf")
    ];

    public async Task<StoredFileView?> UploadAsync(FileUploadCommand command, CancellationToken cancellationToken = default)
    {
        if (!Purposes.Contains(command.Purpose)) throw new InvalidOperationException("Loại tệp không hợp lệ.");
        var projectId = await db.WeddingProjects.AsNoTracking()
            .Where(x => x.ProjectCode == command.ProjectCode && x.ManagerId == command.UploadedBy)
            .Select(x => (int?)x.ProjectId).FirstOrDefaultAsync(cancellationToken);
        if (projectId is null) return null;

        using var buffer = new MemoryStream();
        await command.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 or > MaxBytes) throw new InvalidOperationException("Tệp phải có dung lượng từ 1 byte đến 10 MB (MSG17).");
        var match = DetectType(buffer.GetBuffer());
        if (match.ContentType is null) throw new InvalidOperationException("Chỉ chấp nhận tệp JPG, PNG hoặc PDF hợp lệ (MSG17).");

        var name = Path.GetFileName(command.OriginalName.Replace('\\', '/'));
        var entity = new StoredFile
        {
            FileId = Guid.NewGuid(),
            ProjectId = projectId,
            Purpose = command.Purpose,
            OriginalName = name.Length > 200 ? name[^200..] : name,
            ContentType = match.ContentType,
            SizeBytes = buffer.Length,
            UploadedBy = command.UploadedBy,
            UploadedAt = DateTime.UtcNow
        };
        entity.StorageKey = $"{entity.UploadedAt:yyyy}/{entity.UploadedAt:MM}/{entity.FileId:N}{match.Extension}";

        buffer.Position = 0;
        await storage.UploadAsync(buffer, entity.StorageKey, cancellationToken);
        try
        {
            db.StoredFiles.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(entity.StorageKey, CancellationToken.None);
            throw;
        }
        return new StoredFileView(entity.FileId, entity.Purpose, entity.OriginalName, entity.ContentType, entity.SizeBytes, entity.UploadedAt);
    }

    public async Task<SignedFileUrl?> GetSignedUrlAsync(Guid fileId, int userId, string? role, CancellationToken cancellationToken = default)
    {
        var allowed = await db.StoredFiles.AsNoTracking().AnyAsync(x => x.FileId == fileId && x.Project != null &&
            ((role == "Customer" && x.Project.CustomerId == userId) || (role == "Manager" && x.Project.ManagerId == userId)), cancellationToken);
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
        if (file is null) return null;
        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        return content is null ? null : new FileContent(content, file.ContentType, file.OriginalName);
    }

    public async Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await db.StoredFiles.FirstOrDefaultAsync(x => x.FileId == fileId, cancellationToken);
        if (file is null) return;
        db.StoredFiles.Remove(file);
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(file.StorageKey, cancellationToken);
    }

    private static (byte[] Signature, string ContentType, string Extension) DetectType(byte[] bytes) =>
        AllowedTypes.FirstOrDefault(x => bytes.AsSpan().StartsWith(x.Signature));

    private string Sign(Guid fileId, long expires)
    {
        var payload = Encoding.UTF8.GetBytes($"{fileId:N}:{expires.ToString(CultureInfo.InvariantCulture)}");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), payload));
    }
}
