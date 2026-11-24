using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Infrastructure.Files;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Domain.Entities;
using Xunit;

namespace Tests;

/// <summary>File rules: type by content, phone photos converted, key "{event}/{purpose}/{id}", metadata in StoredFiles, orphan cleanup.</summary>
public sealed class FileServiceTests : IDisposable
{
    // 1×1 JPEG.
    private static readonly byte[] Jpeg = Convert.FromBase64String("/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=");
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");
    // iPhone photo: ISO-BMFF header "....ftypheic".
    private static readonly byte[] Heic = [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'h', (byte)'e', (byte)'i', (byte)'c', 0, 0, 0, 0];
    private static readonly byte[] Webp = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 ");

    private readonly WpsmDbContext _db;
    private readonly FileStorageOptions _options = new() { RootPath = ".", SigningKey = "test-key" };

    public FileServiceTests()
    {
        _db = new WpsmDbContext(new DbContextOptionsBuilder<WpsmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.WeddingProjects.Add(new WeddingProject { ProjectId = 1, ProjectCode = "WPSM-2026-001", Status = "Locked" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private FileService Service(TestFileStorage storage) => new(_db, storage, _options);

    [Fact]
    public async Task Store_NamesTheFileByEventPurposeAndId_AndKeepsMetadata()
    {
        var storage = new TestFileStorage();
        var file = await Service(storage).StoreAsync(1, "ProofPhoto", @"C:\Users\me\IMG_0012.JPG", Jpeg, 7);
        Assert.Matches("^WPSM-2026-001/proof/[0-9a-f]{32}\\.jpg$", file.StorageKey);
        Assert.Equal("IMG_0012.JPG", file.OriginalName);
        var row = _db.StoredFiles.Single();
        Assert.Equal(file.StorageKey, row.StorageKey);
        Assert.Equal("image/jpeg", row.ContentType);
        Assert.True(storage.Files.ContainsKey(file.StorageKey));
    }

    [Fact]
    public async Task Store_PutsReceiptsAndSupplierFilesInTheirFolders()
    {
        var storage = new TestFileStorage();
        Assert.StartsWith("WPSM-2026-001/receipts/", (await Service(storage).StoreAsync(1, "DepositReceipt", "ck.pdf", Pdf, 7)).StorageKey);
        Assert.StartsWith("WPSM-2026-001/suppliers/", (await Service(storage).StoreAsync(1, "SupplierEvidence", "hd.pdf", Pdf, 7)).StorageKey);
    }

    [Fact]
    public async Task Store_IphoneHeic_IsKeptAsJpeg_WhenTheStorageConverts()
    {
        var storage = new TestFileStorage { ConvertsImages = true };
        var file = await Service(storage).StoreAsync(1, "ProofPhoto", "IMG_0012.HEIC", Heic, 7);
        Assert.Equal("image/jpeg", file.ContentType);
        Assert.EndsWith(".jpg", file.StorageKey);
        Assert.Equal("IMG_0012.jpg", file.OriginalName);
        Assert.Equal("image/jpeg", (await Service(storage).StoreAsync(1, "ProofPhoto", "anh.webp", Webp, 7)).ContentType);
    }

    [Fact]
    public async Task Store_IphoneHeic_IsRefusedWithAClearMessage_OnTheLocalDisk()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(new TestFileStorage()).StoreAsync(1, "ProofPhoto", "IMG.HEIC", Heic, 7));
        Assert.Contains("HEIC", error.Message);
        Assert.Empty(_db.StoredFiles);
    }

    [Fact]
    public async Task Store_RefusesUnknownContent_WhateverTheExtension()
    {
        var storage = new TestFileStorage { ConvertsImages = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(storage).StoreAsync(1, "ProofPhoto", "virus.jpg", Encoding.ASCII.GetBytes("MZ fake exe"), 7));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(storage).StoreAsync(1, "ProofPhoto", "big.jpg", new byte[FileService.MaxBytes + 1], 7));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(storage).StoreAsync(1, "Avatar", "a.jpg", Jpeg, 7));
        Assert.Empty(storage.Files);
    }

    [Theory]
    [InlineData("heic"), InlineData("mif1"), InlineData("heix"), InlineData("avif")]
    public void Detect_RecognisesPhonePhotoBrands(string brand)
    {
        var bytes = Heic.ToArray();
        Encoding.ASCII.GetBytes(brand).CopyTo(bytes, 8);
        var kind = FileTypes.Detect(bytes);
        Assert.NotNull(kind);
        Assert.True(kind!.NeedsConversion);
    }

    [Fact]
    public async Task Cleanup_RemovesOnlyOldFilesNoTableRefersTo()
    {
        var storage = new TestFileStorage();
        var kept = await Service(storage).StoreAsync(1, "ProofPhoto", "a.jpg", Jpeg, 7);
        storage.Files["WPSM-2026-001/proof/orphan-old.jpg"] = Jpeg;
        storage.Created["WPSM-2026-001/proof/orphan-old.jpg"] = DateTime.UtcNow.AddDays(-3);
        storage.Files["WPSM-2026-001/proof/orphan-new.jpg"] = Jpeg;
        storage.Created["WPSM-2026-001/proof/orphan-new.jpg"] = DateTime.UtcNow;
        storage.Created[kept.StorageKey] = DateTime.UtcNow.AddDays(-30);

        var removed = await new FileCleanupJob(_db, storage, NullLogger<FileCleanupJob>.Instance).RunAsync(CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.False(storage.Files.ContainsKey("WPSM-2026-001/proof/orphan-old.jpg"));
        Assert.True(storage.Files.ContainsKey("WPSM-2026-001/proof/orphan-new.jpg"));
        Assert.True(storage.Files.ContainsKey(kept.StorageKey));
    }
}
