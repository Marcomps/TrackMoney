using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Infrastructure.Profiles.Repositories;

namespace TrackTraceMoney.Infrastructure.Profiles;

/// <summary>
/// DI registration for the local, password-less multi-profile catalog — mirrors
/// <see cref="InfrastructureServiceCollectionExtensions.AddTrackTraceMoneyInfrastructure"/>'s
/// registration style (lifetimes, patterns), kept as a separate extension method/registration group
/// since this wires up a second, independent <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// against a physically separate SQLite database.
/// </summary>
public static class ProfileCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddTrackTraceMoneyProfileCatalog(
        this IServiceCollection services,
        string profileCatalogConnectionString,
        string profileDataDirectory)
    {
        services.AddDbContext<ProfileCatalogDbContext>(options => options.UseSqlite(profileCatalogConnectionString));

        // Singleton is deliberate, matching AddTrackTraceMoneyInfrastructure's IDbAccessGate
        // registration reasoning -- and a SEPARATE instance from that gate, not a reused one, since
        // this one guards a physically separate SQLite database. See IProfileCatalogDbAccessGate's
        // remarks.
        services.AddSingleton<IProfileCatalogDbAccessGate, ProfileCatalogDbAccessGate>();

        services.AddScoped<ILocalProfileRepository, LocalProfileRepository>();

        // profileDataDirectory is a plain string, not itself a resolvable service, so
        // IProfileManagementService is registered via a factory lambda that captures it (the
        // App-layer caller supplies a real platform path, e.g. FileSystem.AppDataDirectory, when it
        // calls this method).
        services.AddScoped<IProfileManagementService>(sp => new ProfileManagementService(
            sp.GetRequiredService<ProfileCatalogDbContext>(),
            sp.GetRequiredService<ILocalProfileRepository>(),
            sp.GetRequiredService<IActiveProfileStore>(),
            sp.GetRequiredService<IProfileCatalogDbAccessGate>(),
            profileDataDirectory));

        return services;
    }
}
