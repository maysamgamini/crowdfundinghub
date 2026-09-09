using Microsoft.Extensions.Configuration;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Configuration;

/// <summary>
/// Configuration helpers supporting the monolith's evolutionary database-per-service path:
/// single shared connection today, module-specific connection strings whenever an operator
/// chooses to isolate a module's schema onto its own database instance, with zero code changes
/// on either side of that transition.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Resolves a module's connection string, preferring a module-specific key
    /// (<c>ConnectionStrings:{moduleKey}</c>) and falling back to the shared
    /// <c>ConnectionStrings:DefaultConnection</c> when the module-specific key is absent.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="moduleKey">The module-specific connection string key (e.g. <c>"CampaignsDb"</c>).</param>
    /// <param name="fallbackKey">The shared fallback connection string key.</param>
    /// <returns>The resolved connection string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when neither key is configured.</exception>
    public static string GetRequiredModuleConnectionString(
        this IConfiguration configuration,
        string moduleKey,
        string fallbackKey = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(moduleKey)
                               ?? configuration.GetConnectionString(fallbackKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Neither connection string 'ConnectionStrings:{moduleKey}' nor fallback 'ConnectionStrings:{fallbackKey}' was configured.");
        }

        return connectionString;
    }
}
