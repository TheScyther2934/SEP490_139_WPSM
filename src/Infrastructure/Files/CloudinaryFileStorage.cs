using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Persistence;

namespace Infrastructure.Files;

/// <summary>Cloudinary account (Cloudinary__CloudName/ApiKey/ApiSecret in src/API/.env or User Secrets).</summary>
public sealed class CloudinaryOptions
{
    public string? CloudName { get; init; }
    public string? ApiKey { get; init; }
    public string? ApiSecret { get; init; }
    /// <summary>Root folder of this environment, e.g. "wpsm/dev" or "wpsm/prod": several environments share one account.</summary>
    public string Folder { get; init; } = "wpsm/dev";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>
/// File storage on Cloudinary. Rules:
/// - every asset is "private": its public URL answers 401; the browser only gets short-lived signed download URLs from the API;
/// - photos are "image" assets: HEIC/HEIF/WebP are converted to JPEG on upload, the long edge is limited to 2560 px, quality auto;
/// - PDFs are "raw" assets, kept byte for byte;
/// - public id = "{Folder}/{key without extension}" for images, "{Folder}/{key}" for raw files.
/// Talks to the REST API directly (signed requests), the same calls the Cloudinary SDKs make.
/// </summary>
public sealed class CloudinaryFileStorage(CloudinaryOptions options, HttpClient http) : IFileStorage
{
    private const string IncomingTransformation = "c_limit,w_2560,h_2560/q_auto";
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".avif"];

    public bool ConvertsImages => true;

    public async Task<StoredObject> UploadAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default)
    {
        var (resourceType, publicId, extension) = Locate(key);
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId,
            ["type"] = "private",
            ["overwrite"] = "false",
            ["timestamp"] = Now().ToString(CultureInfo.InvariantCulture)
        };
        if (resourceType == "image")
        {
            // JPEG stays JPEG, PNG stays PNG (screenshots, transparency); phone formats become JPEG.
            parameters["format"] = extension == ".png" ? "png" : "jpg";
            parameters["transformation"] = IncomingTransformation;
        }
        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in parameters) form.Add(new StringContent(value), name);
        form.Add(new StringContent(options.ApiKey!), "api_key");
        form.Add(new StringContent(Sign(parameters)), "signature");
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        // The file name is the id, never the user's name for it (kept in StoredFiles.OriginalName only).
        form.Add(file, "file", Path.GetFileName(publicId) + extension);

        using var response = await http.PostAsync($"{ApiBase}/{resourceType}/upload", form, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var bytes = root.GetProperty("bytes").GetInt64();
        if (resourceType == "raw") return new StoredObject(key, contentType, bytes);
        var format = root.GetProperty("format").GetString()!;
        var storedKey = Path.ChangeExtension(key, "." + format)!.Replace('\\', '/');
        return new StoredObject(storedKey, format == "png" ? "image/png" : "image/jpeg", bytes);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(DownloadUrl(key, TimeSpan.FromMinutes(5)), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            return null;
        }
        // Buffered so the caller may dispose it whenever it likes (files are 10 MB at most).
        var buffer = new MemoryStream();
        await using (var body = await response.Content.ReadAsStreamAsync(cancellationToken)) await body.CopyToAsync(buffer, cancellationToken);
        response.Dispose();
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var (resourceType, publicId, _) = Locate(key);
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId, ["type"] = "private", ["invalidate"] = "true", ["timestamp"] = Now().ToString(CultureInfo.InvariantCulture)
        };
        using var form = new FormUrlEncodedContent(parameters.Append(new("api_key", options.ApiKey!)).Append(new("signature", Sign(parameters))));
        using var response = await http.PostAsync($"{ApiBase}/{resourceType}/destroy", form, cancellationToken);
        using var _ = await ReadJsonAsync(response, cancellationToken);
    }

    public Task<string?> GetTemporaryUrlAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(DownloadUrl(key, lifetime));

    /// <summary>Every private asset under this environment's folder (Admin API, paged).</summary>
    public async IAsyncEnumerable<StorageItem> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.ApiKey}:{options.ApiSecret}"));
        foreach (var resourceType in new[] { "image", "raw" })
        {
            string? cursor = null;
            do
            {
                var url = $"{ApiBase}/resources/{resourceType}/private?prefix={Uri.EscapeDataString(options.Folder.TrimEnd('/') + "/")}&max_results=500"
                    + (cursor is null ? "" : $"&next_cursor={Uri.EscapeDataString(cursor)}");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                using var response = await http.SendAsync(request, cancellationToken);
                using var json = await ReadJsonAsync(response, cancellationToken);
                foreach (var item in json.RootElement.GetProperty("resources").EnumerateArray())
                {
                    var publicId = item.GetProperty("public_id").GetString()!;
                    var relative = publicId[(options.Folder.TrimEnd('/').Length + 1)..];
                    var key = resourceType == "image" ? $"{relative}.{item.GetProperty("format").GetString()}" : relative;
                    yield return new StorageItem(key, item.GetProperty("created_at").GetDateTime().ToUniversalTime());
                }
                cursor = json.RootElement.TryGetProperty("next_cursor", out var next) ? next.GetString() : null;
            } while (cursor is not null);
        }
    }

    /// <summary>Signed download address of a private asset; Cloudinary refuses it after <paramref name="lifetime"/>.</summary>
    private string DownloadUrl(string key, TimeSpan lifetime)
    {
        var (resourceType, publicId, extension) = Locate(key);
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId,
            ["type"] = "private",
            ["timestamp"] = Now().ToString(CultureInfo.InvariantCulture),
            ["expires_at"] = (Now() + (long)lifetime.TotalSeconds).ToString(CultureInfo.InvariantCulture)
        };
        if (resourceType == "image") parameters["format"] = extension.TrimStart('.');
        var query = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{ApiBase}/{resourceType}/download?{query}&api_key={options.ApiKey}&signature={Sign(parameters)}";
    }

    private (string ResourceType, string PublicId, string Extension) Locate(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..") || key.StartsWith('/'))
            throw new InvalidOperationException("Khóa lưu trữ không hợp lệ.");
        var extension = Path.GetExtension(key).ToLowerInvariant();
        var folder = options.Folder.TrimEnd('/');
        return ImageExtensions.Contains(extension)
            ? ("image", $"{folder}/{key[..^extension.Length]}", extension)
            : ("raw", $"{folder}/{key}", extension);
    }

    private string ApiBase => $"https://api.cloudinary.com/v1_1/{options.CloudName}";

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Cloudinary signature: SHA-1 of the sorted "key=value" pairs joined by "&", followed by the API secret.
    private string Sign(SortedDictionary<string, string> parameters) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}")) + options.ApiSecret))).ToLowerInvariant();

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = body;
            try { message = JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("message").GetString() ?? body; } catch (JsonException) { }
            throw new InvalidOperationException($"Không lưu được tệp lên Cloudinary: {message}");
        }
        return JsonDocument.Parse(body);
    }
}
