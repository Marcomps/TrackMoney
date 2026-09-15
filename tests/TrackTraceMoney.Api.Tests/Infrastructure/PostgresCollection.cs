namespace TrackTraceMoney.Api.Tests.Infrastructure;

/// <summary>
/// Shares one Testcontainers PostgreSQL instance (and one WebApplicationFactory host) across all
/// integration tests in this collection, instead of paying container-startup cost per test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<ApiWebApplicationFactory>
{
    public const string Name = "Postgres integration tests";
}
