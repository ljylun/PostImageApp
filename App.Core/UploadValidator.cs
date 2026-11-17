namespace PostImageUploader.Core;

/// <summary>
/// Static validation utilities for local file checks.
/// </summary>
public static class UploadValidator
{
    /// <summary>
    /// Validates a local file path.
    /// Returns null if valid, or an UploadResult with error information if invalid.
    /// </summary>
    public static UploadResult? ValidateLocalFile(
        string filePath, 
        IFileSystem fileSystem,
        PostImageOptions options)
    {
        if (!fileSystem.FileExists(filePath))
            return Fail(FailureReason.FileNotFound, $"File not found: {filePath}");

        var ext = fileSystem.GetExtension(filePath).ToLowerInvariant();
        if (!options.SupportedExtensions.Contains(ext))
            return Fail(FailureReason.UnsupportedFormat,
                $"Unsupported format '{ext}'. Supported: " +
                string.Join(", ", options.SupportedExtensions));

        var fileInfo = fileSystem.GetFileInfo(filePath);
        if (fileInfo.Length > options.MaxFileSizeBytes)
            return Fail(FailureReason.FileTooLarge,
                $"File too large: {fileInfo.Length / 1024.0 / 1024.0:F2} MB " +
                $"(PostImages limit is approximately {options.MaxFileSizeBytes / 1024 / 1024} MB)");

        return null; // All checks passed
    }

    /// <summary>
    /// Checks if a URL string starts with http:// or https://.
    /// </summary>
    public static bool IsHttpUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if a URL is a valid postimg.cc URL.
    /// </summary>
    public static bool IsValidPostimgUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.StartsWith("https://postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("http://postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("https://i.postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("http://i.postimg.cc/", StringComparison.OrdinalIgnoreCase));

    internal static UploadResult Fail(FailureReason reason, string message) =>
        new() { Success = false, ErrorMessage = $"Failure: {reason} — {message}" };
}
