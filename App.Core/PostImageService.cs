using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PostImageUploader.Core;

/// <summary>
/// Core service for uploading images to PostImages.org.
/// This class is designed to be host-agnostic and can be used in CLI, Web, or Desktop applications.
/// </summary>
public interface IPostImageService
{
    /// <summary>
    /// Uploads a local image file.
    /// </summary>
    Task<UploadResult> UploadFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an image from a URL and uploads it to PostImages.org.
    /// </summary>
    Task<UploadResult> UploadFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IPostImageService.
/// </summary>
public class PostImageService : IPostImageService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PostImageService> _logger;
    private readonly PostImageOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;

    private bool _sessionReady;
    private bool _disposed;

    public PostImageService(
        IHttpClientFactory httpClientFactory,
        ILogger<PostImageService> logger,
        IOptions<PostImageOptions> options,
        IFileSystem fileSystem,
        TimeProvider timeProvider)
    {
        _httpClient = httpClientFactory.CreateClient("PostImageClient");
        _logger = logger;
        _options = options.Value;
        _fileSystem = fileSystem;
        _timeProvider = timeProvider;
    }

    public async Task<UploadResult> UploadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Local validation
        var validationError = UploadValidator.ValidateLocalFile(filePath, _fileSystem, _options);
        if (validationError is not null) return validationError;

        var ext = _fileSystem.GetExtension(filePath).ToLowerInvariant();
        var fileName = _fileSystem.GetFileName(filePath);

        byte[] bytes;
        try 
        { 
            bytes = await _fileSystem.ReadAllBytesAsync(filePath, cancellationToken); 
        }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.UnknownError, $"Failed to read file: {ex.Message}");
        }

        var mimeType = MultipartBuilder.GetMimeType(ext);
        return await UploadBytesAsync(bytes, fileName, mimeType, bytes.Length, cancellationToken);
    }

    public async Task<UploadResult> UploadFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return UploadValidator.Fail(FailureReason.NetworkError, "URL cannot be empty");

        if (!UploadValidator.IsHttpUrl(imageUrl))
            return UploadValidator.Fail(FailureReason.NetworkError,
                $"Invalid URL format (must start with http:// or https://): {imageUrl}");

        _logger.LogInformation("Downloading remote image: {ImageUrl}", imageUrl);

        try
        {
            using var downloadClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            downloadClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var rsp = await downloadClient.GetAsync(imageUrl, cancellationToken);
            rsp.EnsureSuccessStatusCode();

            var bytes = await rsp.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = rsp.Content.Headers.ContentType?.MediaType ?? "image/png";
            var fileName = Path.GetFileName(new Uri(imageUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
                fileName = $"upload_{_timeProvider.GetUtcNow():yyyyMMddHHmmss}.png";

            _logger.LogInformation("Download complete: {Size} KB, type={Type}, file={File}",
                bytes.Length / 1024.0, contentType, fileName);

            return await UploadBytesAsync(bytes, fileName, contentType, bytes.Length, cancellationToken);
        }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError, $"Failed to download image: {ex.Message}");
        }
    }

    private async Task<UploadResult> UploadBytesAsync(
        byte[] imageBytes, string fileName, string mimeType,
        long originalSize, CancellationToken cancellationToken)
    {
        var result = new UploadResult { FileSizeBytes = originalSize };

        try 
        { 
            await EnsureSessionAsync(cancellationToken); 
        }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError, $"Network connection failed: {ex.Message}");
        }

        _logger.LogDebug("Building multipart request (file: {FileName}, size: {Size} KB)",
            fileName, imageBytes.Length / 1024.0);

        // Generate boundary (Chrome WebKit format)
        var boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N")[..16];

        await using var ms = new MemoryStream();
        MultipartBuilder.WriteField(ms, boundary, "gallery", "");
        MultipartBuilder.WriteField(ms, boundary, "optsize", "0");
        MultipartBuilder.WriteField(ms, boundary, "expire", "0");
        MultipartBuilder.WriteField(ms, boundary, "numfiles", "1");
        MultipartBuilder.WriteField(ms, boundary, "upload_session",
            $"{_timeProvider.GetUtcNow().ToUnixTimeMilliseconds()}.{new Random().Next(10000, 99999)}");

        // File part
        MultipartBuilder.WriteTextLine(ms, $"--{boundary}");
        MultipartBuilder.WriteTextLine(ms, $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"");
        MultipartBuilder.WriteTextLine(ms, $"Content-Type: {mimeType}");
        MultipartBuilder.WriteTextLine(ms);
        ms.Write(imageBytes);
        MultipartBuilder.WriteTextLine(ms);
        MultipartBuilder.WriteTextLine(ms, $"--{boundary}--");

        var bodyBytes = ms.ToArray();
        var contentType = $"multipart/form-data; boundary={boundary}";

        _logger.LogDebug("Request body size: {Size} KB", bodyBytes.Length / 1024.0);
        _logger.LogInformation("Uploading to postimages.org...");

        // Send request
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.UploadApiUrl);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Origin", "https://postimages.org");
        request.Headers.Add("Referer", "https://postimages.org/");
        request.Headers.Add("Cache-Control", "no-cache");
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var uploadSw = System.Diagnostics.Stopwatch.StartNew();
        HttpResponseMessage httpRsp;
        try
        {
            httpRsp = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return UploadValidator.Fail(FailureReason.Timeout, "Request timeout (> 120 seconds)");
        }
        catch (HttpRequestException ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError,
                $"Network error: {ex.Message}" +
                (ex.InnerException != null ? $" (Inner: {ex.InnerException.Message})" : ""));
        }
        uploadSw.Stop();
        result.UploadElapsed = uploadSw.Elapsed;

        var rawJson = await httpRsp.Content.ReadAsStringAsync(cancellationToken);
        result.HttpStatusCode = (int)httpRsp.StatusCode;
        result.RawResponse = rawJson;

        _logger.LogDebug("HTTP {StatusCode}, duration: {Duration}s", result.HttpStatusCode, result.UploadElapsed.TotalSeconds);
        _logger.LogDebug("Response: {Response}", rawJson);

        // Validation 1: HTTP status code
        if (!httpRsp.IsSuccessStatusCode)
        {
            string hint = result.HttpStatusCode switch
            {
                400 => "Invalid request parameters",
                403 => "Permission error (may need Cookie or rate limited)",
                413 => "File too large",
                429 => "Too many requests, rate limited",
                500 => "Server internal error",
                503 => "Service temporarily unavailable",
                _ => "Unknown HTTP error"
            };
            return new UploadResult
            {
                HttpStatusCode = result.HttpStatusCode,
                RawResponse = rawJson,
                UploadElapsed = result.UploadElapsed,
                FileSizeBytes = originalSize,
                Success = false,
                ErrorMessage = $"HTTP {result.HttpStatusCode} ({hint}): {rawJson}"
            };
        }

        // Validation 2: Non-empty response body
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            result.Success = false;
            result.ErrorMessage = $"Failure: {FailureReason.EmptyResponse} — Server returned empty response";
            return result;
        }

        // Validation 3: JSON parsing
        PostImageApiResponse? apiResp;
        try
        {
            apiResp = JsonSerializer.Deserialize<PostImageApiResponse>(rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failure: {FailureReason.JsonParseError} — JSON parse error: {ex.Message} | Raw: {rawJson}";
            return result;
        }

        if (apiResp is null)
        {
            result.Success = false;
            result.ErrorMessage = $"Failure: {FailureReason.JsonParseError} — Deserialization result is null | Raw: {rawJson}";
            return result;
        }

        // Validation 4: URL validity
        if (!apiResp.IsValidUrl)
        {
            result.Success = false;
            result.ErrorMessage = $"Failure: {FailureReason.InvalidResponseUrl} — Invalid URL returned: '{apiResp.Url}' | Raw: {rawJson}";
            return result;
        }

        var pageUrl = apiResp.Url!;
        result.PageUrl = pageUrl;

        _logger.LogInformation("Fetching page HTML to parse direct image URL: {PageUrl}", pageUrl);
        var verifySw = System.Diagnostics.Stopwatch.StartNew();

        // Parse direct URL from HTML
        var (directUrl, parseError) = await FetchAndParseDirectUrlAsync(pageUrl, cancellationToken);
        if (parseError is not null || directUrl is null)
        {
            verifySw.Stop();
            result.VerifyElapsed = verifySw.Elapsed;
            result.Success = false;
            result.ErrorMessage = $"Failure: {FailureReason.InvalidResponseUrl} — {parseError}";
            return result;
        }

        result.DirectImageUrl = directUrl;
        result.ImageUrl = directUrl;

        // Verify link accessibility
        _logger.LogInformation("Verifying direct link accessibility: {ImageUrl}", result.ImageUrl);
        (result.LinkAccessible, var accessError) = await VerifyLinkAsync(result.ImageUrl!, cancellationToken);
        verifySw.Stop();
        result.VerifyElapsed = verifySw.Elapsed;

        if (!result.LinkAccessible)
        {
            result.Success = false;
            result.ErrorMessage = $"Failure: {FailureReason.LinkNotAccessible} — Direct link not accessible: {accessError}";
            return result;
        }

        _logger.LogInformation("Direct link verified successfully (duration: {Duration}s)", result.VerifyElapsed.TotalSeconds);
        result.Success = true;
        return result;
    }

    private async Task EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (_sessionReady) return;

        _logger.LogDebug("Establishing session (fetching homepage Cookie)...");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _options.HomeUrl);
            req.Headers.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9," +
                "image/avif,image/webp,image/apng,*/*;q=0.8");
            req.Headers.Add("Upgrade-Insecure-Requests", "1");
            await _httpClient.SendAsync(req, cancellationToken);
            _logger.LogDebug("Session established successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Homepage access failed (continuing with upload attempt): {Message}", ex.Message);
        }
        _sessionReady = true;
    }

    private async Task<(bool ok, string? error)> VerifyLinkAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var verifyClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_options.VerificationTimeoutSeconds) };
            verifyClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var headResp = await verifyClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, url), cancellationToken);

            if (headResp.IsSuccessStatusCode)
            {
                _logger.LogDebug("Link accessible (HTTP {StatusCode})", (int)headResp.StatusCode);
                return (true, null);
            }

            // Fallback to GET
            var getResp = await verifyClient.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (getResp.IsSuccessStatusCode)
            {
                _logger.LogDebug("Link accessible via GET (HTTP {StatusCode})", (int)getResp.StatusCode);
                return (true, null);
            }

            return (false, $"HTTP {(int)getResp.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return (false, $"Link verification timeout (>{_options.VerificationTimeoutSeconds}s)");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(string? directUrl, string? error)> FetchAndParseDirectUrlAsync(
        string pageUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var verifyClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            verifyClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await verifyClient.GetAsync(pageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {(int)response.StatusCode} when fetching page HTML");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var directUrl = ParseDirectImageUrl(html);
            if (string.IsNullOrWhiteSpace(directUrl))
            {
                return (null, "Failed to parse direct image URL from HTML page");
            }

            return (directUrl, null);
        }
        catch (Exception ex)
        {
            return (null, $"Error fetching page HTML: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the direct image URL from the HTML page.
    /// </summary>
    public static string? ParseDirectImageUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        // Method 1: parse input id="direct" value="xxx"
        var directMatch = System.Text.RegularExpressions.Regex.Match(html,
            @"id=""direct""\s+value=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (directMatch.Success) return directMatch.Groups[1].Value;

        // Method 2: parse <meta property="og:image" content="xxx">
        var ogMatch = System.Text.RegularExpressions.Regex.Match(html,
            @"<meta\s+property=""og:image""\s+content=""([^""]+)""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (ogMatch.Success) return ogMatch.Groups[1].Value;

        // Method 3: parse domain i.postimg.cc link pattern
        var fallbackMatch = System.Text.RegularExpressions.Regex.Match(html,
            @"https?://i\.postimg\.cc/[a-zA-Z0-9]+/[^""\s>]+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (fallbackMatch.Success) return fallbackMatch.Value;

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}
