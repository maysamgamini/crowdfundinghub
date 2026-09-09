using CrowdFunding.BuildingBlocks.Infrastructure.Configuration;
using CrowdFunding.Modules.Identity.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.DbContexts;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.Repositories;
using CrowdFunding.Modules.Identity.Infrastructure.Services;
using CrowdFunding.Modules.Identity.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.Modules.Identity.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services from the surrounding layer with the dependency injection container.
/// </summary>
public static class IdentityInfrastructureDependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredModuleConnectionString("IdentityDb");
        var redisConnectionString = configuration.GetConnectionString("Redis")
                                    ?? throw new InvalidOperationException("Connection string 'Redis' was not found.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        // TICKET-036: the distributed revocation blacklist (RedisSecurityStampRevocationStore)
        // needs IDistributedCache. AddStackExchangeRedisCache is idempotent to call once per
        // module (see CampaignsInfrastructureDependencyInjection) — the last registration's
        // options win, but they all point at the same Redis instance/connection string.
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "crowdfunding:";
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IIdentityTransactionExecutor, IdentityTransactionExecutor>();
        services.AddScoped<IAccessTokenProvider, JwtAccessTokenProvider>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<ISecurityStampRevocationStore, RedisSecurityStampRevocationStore>();
        services.AddSingleton<IIdentityDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ISigningKeyStore, EfSigningKeyStore>();

        return services;
    }
}
