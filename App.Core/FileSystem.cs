using System.Text;

namespace PostImageUploader.Core;

/// <summary>
/// Default implementation of IFileSystem using System.IO.
/// </summary>
public class FileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public IFileInfo GetFileInfo(string path) => new FileInfoWrapper(new FileInfo(path));

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        => await File.ReadAllBytesAsync(path, cancellationToken);

    public string GetExtension(string path) => Path.GetExtension(path);

    public string GetFileName(string path) => Path.GetFileName(path);

    public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
}

/// <summary>
/// Wrapper for FileInfo to implement IFileInfo.
/// </summary>
public class FileInfoWrapper : IFileInfo
{
    private readonly FileInfo _fileInfo;

    public FileInfoWrapper(FileInfo fileInfo)
    {
        _fileInfo = fileInfo;
    }

    public long Length => _fileInfo.Length;
}

/// <summary>
/// Utilities for building multipart/form-data requests.
/// </summary>
public static class MultipartBuilder
{
    public static void WriteField(Stream stream, string boundary, string name, string value)
    {
        WriteTextLine(stream, $"--{boundary}");
        WriteTextLine(stream, $"Content-Disposition: form-data; name=\"{name}\"");
        WriteTextLine(stream);
        WriteTextLine(stream, value);
    }

    public static void WriteTextLine(Stream stream, string text = "")
    {
        var bytes = Encoding.UTF8.GetBytes(text + "\r\n");
        stream.Write(bytes);
    }

    public static string GetMimeType(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif"            => "image/gif",
        ".bmp"            => "image/bmp",
        ".webp"           => "image/webp",
        ".tiff" or ".tif" => "image/tiff",
        _                 => "image/png",
    };
}
