namespace PostImageUploader.Core;

/// <summary>
/// Abstraction for file system operations to enable testability and decoupling.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Gets file information for the specified path.
    /// </summary>
    IFileInfo GetFileInfo(string path);

    /// <summary>
    /// Reads all bytes from a file asynchronously.
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the extension of a file path.
    /// </summary>
    string GetExtension(string path);

    /// <summary>
    /// Gets the file name from a path.
    /// </summary>
    string GetFileName(string path);

    /// <summary>
    /// Gets the file name without extension from a path.
    /// </summary>
    string GetFileNameWithoutExtension(string path);
}

/// <summary>
/// Abstraction for file information.
/// </summary>
public interface IFileInfo
{
    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    long Length { get; }
}
