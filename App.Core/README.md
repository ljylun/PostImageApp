# PostImageUploader.Core

A reusable .NET library for uploading images to PostImages.org. This library provides core business logic without any CLI or hosting dependencies, making it suitable for use in console applications, web applications, desktop applications, and more.

## Features

- Upload images from local files
- Upload images from remote URLs
- Strongly-typed configuration options
- Dependency injection support
- Logging integration via `ILogger<T>`
- Testable abstractions for file system and time operations
- No direct dependencies on CLI parsing libraries

## Installation

Install via NuGet Package Manager:

```powershell
Install-Package PostImageUploader.Core
```

Or via .NET CLI:

```bash
dotnet add package PostImageUploader.Core
```

## Quick Start

### Basic Usage with Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PostImageUploader.Core;

// Setup service collection
var services = new ServiceCollection();

// Add logging (optional but recommended)
services.AddLogging(builder => builder.AddConsole());

// Register PostImageUploader core services
services.AddPostImageUploaderCore();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Resolve and use the service
var uploadService = serviceProvider.GetRequiredService<IPostImageService>();
var result = await uploadService.UploadFileAsync("path/to/image.jpg");

if (result.Success)
{
    Console.WriteLine($"Upload successful: {result.ImageUrl}");
}
else
{
    Console.Error.WriteLine($"Upload failed: {result.ErrorMessage}");
}
```

### Configuration Options

You can configure the library using `IConfiguration`:

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

services.AddPostImageUploaderCore(configuration);
```

Example `appsettings.json`:

```json
{
  "PostImageOptions": {
    "MaxFileSizeBytes": 12582912,
    "RequestTimeoutSeconds": 120,
    "VerificationTimeoutSeconds": 15,
    "SupportedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif"]
  }
}
```

Or configure programmatically:

```csharp
services.AddPostImageUploaderCore(options =>
{
    options.MaxFileSizeBytes = 12L * 1024 * 1024; // 12 MB
    options.RequestTimeoutSeconds = 120;
});
```

### Uploading from a URL

```csharp
var result = await uploadService.UploadFromUrlAsync("https://example.com/image.png");

if (result.Success)
{
    Console.WriteLine($"Upload successful: {result.ImageUrl}");
}
```

## API Reference

### IPostImageService

The main interface for uploading images:

```csharp
public interface IPostImageService
{
    Task<UploadResult> UploadFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<UploadResult> UploadFromUrlAsync(string imageUrl, CancellationToken cancellationToken = default);
}
```

### UploadResult

Represents the result of an upload operation:

| Property | Type | Description |
|----------|------|-------------|
| Success | bool | Whether the upload was successful |
| ImageUrl | string? | The final image URL |
| DirectImageUrl | string? | The direct image URL parsed from HTML |
| PageUrl | string? | The original page URL from the API |
| LinkAccessible | bool | Whether the link accessibility check passed |
| ErrorMessage | string? | Error message if failed |
| HttpStatusCode | int | HTTP status code |
| RawResponse | string? | Raw response body for debugging |
| FileSizeBytes | long | File size in bytes |
| UploadElapsed | TimeSpan | Upload duration |
| VerifyElapsed | TimeSpan | Link verification duration |

### PostImageOptions

Configuration options:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| MaxFileSizeBytes | long | 12 MB | Maximum file size |
| SupportedExtensions | string[] | [.jpg, .jpeg, ...] | Supported file extensions |
| RequestTimeoutSeconds | int | 120 | HTTP request timeout |
| VerificationTimeoutSeconds | int | 15 | Link verification timeout |
| UploadApiUrl | string | postimages.org/json | API endpoint URL |
| HomeUrl | string | postimages.org/ | Homepage URL for session |

## Advanced Usage

### Custom File System Implementation

For testing or special scenarios, you can provide a custom `IFileSystem` implementation:

```csharp
public class MockFileSystem : IFileSystem
{
    public bool FileExists(string path) => true;
    public IFileInfo GetFileInfo(string path) => new MockFileInfo(1024);
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default) 
        => Task.FromResult(new byte[1024]);
    public string GetExtension(string path) => ".png";
    public string GetFileName(string path) => "test.png";
    public string GetFileNameWithoutExtension(string path) => "test";
}

services.AddSingleton<IFileSystem, MockFileSystem>();
```

### Using in ASP.NET Core

```csharp
// Program.cs
builder.Services.AddPostImageUploaderCore(builder.Configuration);

// In your controller or service
public class ImageController : ControllerBase
{
    private readonly IPostImageService _uploadService;

    public ImageController(IPostImageService uploadService)
    {
        _uploadService = uploadService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        // Save file temporarily and upload
        var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
        await using (var stream = new FileStream(tempPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var result = await _uploadService.UploadFileAsync(tempPath);
        
        if (result.Success)
        {
            return Ok(new { url = result.ImageUrl });
        }
        
        return BadRequest(result.ErrorMessage);
    }
}
```

## Error Handling

The library returns detailed error information through the `UploadResult` object:

```csharp
var result = await uploadService.UploadFileAsync("invalid.jpg");

if (!result.Success)
{
    Console.Error.WriteLine($"Error: {result.ErrorMessage}");
    Console.Error.WriteLine($"HTTP Status: {result.HttpStatusCode}");
    Console.Error.WriteLine($"Failure Reason: Check FailureReason enum");
}
```

## Thread Safety

- `IPostImageService` implementations are designed to be used within a scoped lifetime
- Create a new scope for each logical operation when using in long-running applications
- The underlying `HttpClient` is managed by `IHttpClientFactory` for optimal connection pooling

## License

MIT License
