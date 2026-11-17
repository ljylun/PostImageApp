using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PostImageUploader.Core;

// ═══════════════════════════════════════════════════════════════════════════════
//  PostImage Uploader — CLI Entry Point (Refactored)
//
//  Usage:
//    postimageuploader <filepath>          Upload a local image file
//    postimageuploader --file <filepath>   Upload a local image file (explicit)
//    postimageuploader --url <imageurl>    Upload from a remote image URL
//    postimageuploader --help              Show usage information
//
//  Exit codes:
//    0 — Success (image URL printed to stdout)
//    1 — Failure (error description printed to stderr)
//    2 — Invalid arguments
// ═══════════════════════════════════════════════════════════════════════════════

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Setup command-line parsing using System.CommandLine
var fileOption = new Option<string?>(
    aliases: ["--file", "-f"],
    description: "Path to a local image file to upload");

var urlOption = new Option<string?>(
    aliases: ["--url", "-u"],
    description: "URL of a remote image to upload");

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Enable verbose output",
    getDefaultValue: () => false);

var helpOption = new Option<bool>(
    aliases: ["--help", "-h"],
    description: "Show help information",
    getDefaultValue: () => false);

var positionalArg = new Argument<string?>("filepath", "Path to a local image file").LegalFilePathsOnly();
positionalArg.Arity = ArgumentArity.ZeroOrOne;

var rootCommand = new RootCommand("PostImage Uploader — Upload images to PostImages.org")
{
    fileOption,
    urlOption,
    verboseOption,
    helpOption,
    positionalArg
};

rootCommand.SetHandler(async (invocationContext) =>
{
    var filePath = invocationContext.ParseResult.GetValueForOption(fileOption);
    var imageUrl = invocationContext.ParseResult.GetValueForOption(urlOption);
    var verbose = invocationContext.ParseResult.GetValueForOption(verboseOption);
    var showHelp = invocationContext.ParseResult.GetValueForOption(helpOption);
    var positionalValue = invocationContext.ParseResult.GetValueForArgument(positionalArg);

    // Handle help
    if (showHelp)
    {
        PrintUsage(invocationContext.Console);
        invocationContext.ExitCode = 0;
        return;
    }

    // Auto-detect positional argument
    if (filePath is null && positionalValue is not null)
    {
        if (positionalValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            positionalValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            imageUrl = positionalValue;
        }
        else
        {
            filePath = positionalValue;
        }
    }

    // Validate mutually exclusive inputs
    if (filePath is not null && imageUrl is not null)
    {
        WriteError(invocationContext.Console, "Cannot specify both --file and --url. Please provide only one input.");
        invocationContext.ExitCode = 2;
        return;
    }

    if (filePath is null && imageUrl is null)
    {
        WriteError(invocationContext.Console, "No input provided. Use --file <path> or --url <url>.");
        PrintUsage(invocationContext.Console);
        invocationContext.ExitCode = 2;
        return;
    }

    // Build service provider and execute
    try
    {
        var serviceProvider = BuildServiceProvider(verbose);
        var uploadService = serviceProvider.GetRequiredService<IPostImageService>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        UploadResult result;
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        try
        {
            if (filePath is not null)
            {
                logger.LogInformation("Uploading local file: {FilePath}", filePath);
                result = await uploadService.UploadFileAsync(filePath, cts.Token);
            }
            else
            {
                logger.LogInformation("Uploading from URL: {ImageUrl}", imageUrl);
                result = await uploadService.UploadFromUrlAsync(imageUrl!, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            WriteError(invocationContext.Console, "Operation timed out (> 3 minutes).");
            invocationContext.ExitCode = 1;
            return;
        }
        catch (Exception ex)
        {
            WriteError(invocationContext.Console, $"Unexpected error: {ex.Message}");
            invocationContext.ExitCode = 1;
            return;
        }

        // Output result
        if (result.Success && !string.IsNullOrWhiteSpace(result.ImageUrl))
        {
            // Clean stdout: just the URL — suitable for scripting / piping
            invocationContext.Console.Out.WriteLine(result.ImageUrl);
            invocationContext.ExitCode = 0;
        }
        else
        {
            WriteError(invocationContext.Console, result.ErrorMessage ?? "Upload failed for unknown reason.");
            invocationContext.ExitCode = 1;
        }
    }
    catch (Exception ex)
    {
        WriteError(invocationContext.Console, $"Fatal error: {ex.Message}");
        invocationContext.ExitCode = 1;
    }
});

return await rootCommand.InvokeAsync(args);

// ── Helpers ───────────────────────────────────────────────────────────────────

static IServiceProvider BuildServiceProvider(bool verbose)
{
    var services = new ServiceCollection();

    // Configuration
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .Build();
    services.AddSingleton<IConfiguration>(configuration);

    // Logging
    services.AddLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Warning;
        });
        if (verbose)
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        }
        else
        {
            builder.SetMinimumLevel(LogLevel.Information);
        }
    });

    // Register PostImageUploader Core
    services.AddPostImageUploaderCore(configuration);

    return services.BuildServiceProvider();
}

static void WriteError(IConsole console, string message)
{
    console.Error.WriteLine($"[ERROR] {message}");
}

static void PrintUsage(IConsole console)
{
    console.Out.WriteLine("PostImage Uploader — Upload images to PostImages.org");
    console.Out.WriteLine();
    console.Out.WriteLine("Usage:");
    console.Out.WriteLine("  postimageuploader <filepath>          Upload a local file (positional)");
    console.Out.WriteLine("  postimageuploader --file <filepath>   Upload a local file");
    console.Out.WriteLine("  postimageuploader --url <imageurl>    Upload from a remote URL");
    console.Out.WriteLine("  postimageuploader --verbose           Enable verbose output");
    console.Out.WriteLine("  postimageuploader --help              Show this help");
    console.Out.WriteLine();
    console.Out.WriteLine("Supported formats:  .jpg .jpeg .png .gif .bmp .webp .tiff .tif");
    console.Out.WriteLine("Max file size:      12 MB");
    console.Out.WriteLine();
    console.Out.WriteLine("Output:");
    console.Out.WriteLine("  On success: prints the permanent postimg.cc URL to stdout");
    console.Out.WriteLine("  On failure: prints error reason to stderr, exits with code 1");
    console.Out.WriteLine();
    console.Out.WriteLine("Examples:");
    console.Out.WriteLine("  postimageuploader photo.jpg");
    console.Out.WriteLine("  postimageuploader --file ./images/banner.png");
    console.Out.WriteLine("  postimageuploader --url https://example.com/image.png");
}
