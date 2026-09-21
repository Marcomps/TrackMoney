using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <see cref="CategoryRepository"/> -- round-trips
/// <see cref="Category.IsActive"/>/<see cref="Category.Icon"/> and <c>GetActiveAsync</c>'s filter
/// (Category lifecycle slice §A.3), mirroring <c>RecurringIncomeRepositoryTests</c>' shape.
/// </summary>
public sealed class CategoryRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public CategoryRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsIconAndIsActive()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();

        var category = Category.CreateUserDefined("Mascotas", icon: "🐕");

        await repository.AddAsync(category);
        await repository.SaveChangesAsync();

        var reloaded = await repository.GetByIdAsync(category.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Mascotas", reloaded!.Name);
        Assert.Equal("🐕", reloaded.Icon);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task GetActiveAsync_FiltersOutDeactivatedEntries()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICategoryRepository>();

        var active = Category.CreateUserDefined("Mascotas");
        var inactive = Category.CreateUserDefined("Viejo hobby");
        inactive.Deactivate();

        await repository.AddAsync(active);
        await repository.AddAsync(inactive);
        await repository.SaveChangesAsync();

        var result = await repository.GetActiveAsync();

        var single = Assert.Single(result);
        Assert.Equal(active.Id, single.Id);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
