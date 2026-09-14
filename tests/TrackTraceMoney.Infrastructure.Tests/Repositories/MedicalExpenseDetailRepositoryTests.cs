using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Tests.Repositories;

/// <summary>
/// Real-SQLite-backed coverage for <see cref="MedicalExpenseDetailRepository"/> (README §25/§26/§27/
/// §29) — exercises the actual unique index on TransactionId, which an in-memory fake can't verify.
/// </summary>
public sealed class MedicalExpenseDetailRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tracktracemoney-tests-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _provider;

    public MedicalExpenseDetailRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddTrackTraceMoneyInfrastructure($"Data Source={_dbPath}");
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackTraceMoneyDbContext>();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task DuplicateTransactionIdInsert_BypassingTheService_Throws()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMedicalExpenseDetailRepository>();
        var transactionId = Guid.NewGuid();

        await repository.AddAsync(new MedicalExpenseDetail(transactionId, "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending));
        await repository.SaveChangesAsync();

        await repository.AddAsync(new MedicalExpenseDetail(transactionId, "Other Insurance", 200m, 50m, MedicalReimbursementStatus.Pending));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
    }

    [Fact]
    public async Task GetForTransactionAsync_FindsMatch_AndReturnsNullOtherwise()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMedicalExpenseDetailRepository>();
        var transactionId = Guid.NewGuid();

        await repository.AddAsync(new MedicalExpenseDetail(transactionId, "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending));
        await repository.SaveChangesAsync();

        var found = await repository.GetForTransactionAsync(transactionId);
        var notFound = await repository.GetForTransactionAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal("Acme Insurance", found!.InsuranceProvider);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task GetForTransactionsAsync_ReturnsCorrectDictionary_OmittingTransactionsWithNoDetailRow()
    {
        using var scope = _provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMedicalExpenseDetailRepository>();

        var transactionWithDetail1 = Guid.NewGuid();
        var transactionWithDetail2 = Guid.NewGuid();
        var transactionWithNoDetail = Guid.NewGuid();

        await repository.AddAsync(new MedicalExpenseDetail(transactionWithDetail1, "Acme Insurance", 100m, 40m, MedicalReimbursementStatus.Pending));
        await repository.AddAsync(new MedicalExpenseDetail(transactionWithDetail2, null, null, null, MedicalReimbursementStatus.None));
        await repository.SaveChangesAsync();

        var result = await repository.GetForTransactionsAsync([transactionWithDetail1, transactionWithDetail2, transactionWithNoDetail]);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(transactionWithDetail1));
        Assert.True(result.ContainsKey(transactionWithDetail2));
        Assert.False(result.ContainsKey(transactionWithNoDetail));
        Assert.Equal(MedicalReimbursementStatus.Pending, result[transactionWithDetail1].Status);
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
