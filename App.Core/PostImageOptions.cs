namespace PostImageUploader.Core;

/// <summary>
/// Configuration options for the PostImageUploader client.
/// </summary>
public class PostImageOptions
{
    /// <summary>
    /// Maximum file size in bytes (default: 12 MB).
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 12L * 1024 * 1024;

    /// <summary>
    /// Supported file extensions.
    /// </summary>
    public string[] SupportedExtensions { get; set; } =
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif"];

    /// <summary>
    /// HTTP request timeout in seconds (default: 120).
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Link verification timeout in seconds (default: 15).
    /// </summary>
    public int VerificationTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// The upload API endpoint URL.
    /// </summary>
    public string UploadApiUrl { get; set; } = "https://postimages.org/json";

    /// <summary>
    /// The homepage URL for session initialization.
    /// </summary>
    public string HomeUrl { get; set; } = "https://postimages.org/";
}
