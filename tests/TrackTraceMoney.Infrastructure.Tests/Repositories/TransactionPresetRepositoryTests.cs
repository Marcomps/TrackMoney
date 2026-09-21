using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.TransactionPresets;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <see cref="TransactionPresetRepository"/> -- round-trips every field,
/// including both nullable account-id fields (Transaction Type Customization slice spec §B.6/§B.7.5),
/// and <c>GetActiveAsync</c>'s filter, mirroring <c>CategoryRepositoryTests</c>' shape.
/// </summary>
public sealed class TransactionPresetRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public TransactionPresetRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure(_ => $"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsPlainAccountPreset()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionPresetRepository>();

        var categoryId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var preset = new TransactionPreset("Gasolina", "⛽", TransactionPresetBaseType.Expense, categoryId, accountId, null);

        await repository.AddAsync(preset);
        await repository.SaveChangesAsync();

        var reloaded = await repository.GetByIdAsync(preset.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Gasolina", reloaded!.Name);
        Assert.Equal("⛽", reloaded.Icon);
        Assert.Equal(TransactionPresetBaseType.Expense, reloaded.BaseType);
        Assert.Equal(categoryId, reloaded.DefaultCategoryId);
        Assert.Equal(accountId, reloaded.DefaultAccountId);
        Assert.Null(reloaded.DefaultCreditAccountId);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsCardBackedPreset()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionPresetRepository>();

        var creditAccountId = Guid.NewGuid();
        var preset = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, creditAccountId);

        await repository.AddAsync(preset);
        await repository.SaveChangesAsync();

        var reloaded = await repository.GetByIdAsync(preset.Id);

        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.DefaultAccountId);
        Assert.Equal(creditAccountId, reloaded.DefaultCreditAccountId);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsNoDefaultAccountAtAll()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionPresetRepository>();

        var preset = new TransactionPreset("Recordatorio", null, TransactionPresetBaseType.Income, null, null, null);

        await repository.AddAsync(preset);
        await repository.SaveChangesAsync();

        var reloaded = await repository.GetByIdAsync(preset.Id);

        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.DefaultAccountId);
        Assert.Null(reloaded.DefaultCreditAccountId);
        Assert.Null(reloaded.DefaultCategoryId);
    }

    [Fact]
    public async Task GetActiveAsync_FiltersOutDeactivatedEntries()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITransactionPresetRepository>();

        var active = new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null);
        var inactive = new TransactionPreset("Viejo preset", null, TransactionPresetBaseType.Expense, null, null, null);
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
