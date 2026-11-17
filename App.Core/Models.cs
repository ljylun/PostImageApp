using System.Net;

namespace PostImageUploader.Core;

/// <summary>
/// Response model from the PostImages API.
/// </summary>
public class PostImageApiResponse
{
    /// <summary>
    /// The URL returned by the API (postimg.cc page URL).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// The image identifier or alternative URL.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Whether the returned URL is a valid postimg.cc link.
    /// </summary>
    public bool IsValidUrl =>
        !string.IsNullOrWhiteSpace(Url) &&
        (Url.StartsWith("https://postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         Url.StartsWith("http://postimg.cc/", StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Result of an upload operation.
/// </summary>
public class UploadResult
{
    /// <summary>
    /// Whether the overall operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The final image URL (direct link to the image).
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// The direct image URL parsed from the HTML page.
    /// </summary>
    public string? DirectImageUrl { get; set; }

    /// <summary>
    /// The original page URL returned by the API.
    /// </summary>
    public string? PageUrl { get; set; }

    /// <summary>
    /// Whether the link accessibility check passed.
    /// </summary>
    public bool LinkAccessible { get; set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// HTTP status code (0 for local errors).
    /// </summary>
    public int HttpStatusCode { get; set; }

    /// <summary>
    /// Raw response body for debugging.
    /// </summary>
    public string? RawResponse { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Upload duration.
    /// </summary>
    public TimeSpan UploadElapsed { get; set; }

    /// <summary>
    /// Link verification duration.
    /// </summary>
    public TimeSpan VerifyElapsed { get; set; }
}

/// <summary>
/// Enumeration of failure reasons.
/// </summary>
public enum FailureReason
{
    None,
    FileNotFound,
    FileTooLarge,
    UnsupportedFormat,
    NetworkError,
    Timeout,
    HttpError,
    JsonParseError,
    InvalidResponseUrl,
    LinkNotAccessible,
    EmptyResponse,
    UnknownError,
}
