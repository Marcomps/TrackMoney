using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TrackTraceMoney.Api.Persistence;

namespace TrackTraceMoney.Api.Tests.Infrastructure;

/// <summary>
/// Spins up a real, ephemeral PostgreSQL container (Testcontainers) per test run and points
/// TrackTraceMoney.Api's cloud DbContext at it — per this repo's "prefer real backing stores over
/// mocks" convention (see the Infrastructure test suite's real-SQLite precedent). Requires Docker
/// running locally; if Docker isn't available, <see cref="InitializeAsync"/> will fail/hang and
/// tests using this fixture cannot run in that environment.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tracktracemoney_test")
        .WithUsername("tracktracemoney_test")
        .WithPassword("tracktracemoney_test_only")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Force the host to build now (rather than lazily on the first HTTP request) so the
        // migration below runs against the container's connection string.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyCloudDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CloudDatabase"] = _container.GetConnectionString(),
                ["Jwt:Key"] = "integration-test-only-signing-key-not-a-real-secret-0123456789",
                ["Jwt:Issuer"] = "TrackTraceMoney.Api",
                ["Jwt:Audience"] = "TrackTraceMoney.App",
                ["Jwt:ExpiryDays"] = "30",
            });
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}
