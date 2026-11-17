using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace PostImageUploader.Core;

/// <summary>
/// Extension methods for registering PostImageUploader services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PostImageUploader core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional configuration for PostImageOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostImageUploaderCore(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Register options
        if (configuration is not null)
        {
            services.Configure<PostImageOptions>(
                configuration.GetSection("PostImageOptions"));
        }
        else
        {
            services.Configure<PostImageOptions>(options =>
            {
                // Default values are already set in PostImageOptions class
            });
        }

        // Register file system abstraction
        services.AddSingleton<IFileSystem, FileSystem>();

        // Register time provider
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // Register HTTP client factory and configure the named client
        services.AddHttpClient("PostImageClient", (provider, client) =>
        {
            var options = provider.GetRequiredService<System.IOptions<PostImageOptions>>();
            client.Timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);

            // Simulate Chrome browser headers
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/148.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept-Language",
                "zh-CN,zh;q=0.9,en;q=0.8");
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
            };
        });

        // Register the main service
        services.AddScoped<IPostImageService, PostImageService>();

        return services;
    }

    /// <summary>
    /// Adds PostImageUploader core services with custom options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure PostImageOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostImageUploaderCore(
        this IServiceCollection services,
        Action<PostImageOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddPostImageUploaderCore(null);
    }
}
