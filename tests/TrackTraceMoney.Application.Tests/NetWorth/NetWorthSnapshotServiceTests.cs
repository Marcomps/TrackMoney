using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.NetWorth;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.NetWorth;

namespace TrackTraceMoney.Application.Tests.NetWorth;

public sealed class NetWorthSnapshotServiceTests
{
    private static (NetWorthSnapshotService Service, InMemoryFinancialAccountRepository Accounts, InMemoryCreditAccountRepository CreditAccounts, InMemoryNetWorthSnapshotRepository Snapshots) CreateSut()
    {
        var accounts = new InMemoryFinancialAccountRepository();
        var creditAccounts = new InMemoryCreditAccountRepository();
        var snapshots = new InMemoryNetWorthSnapshotRepository();
        var service = new NetWorthSnapshotService(accounts, creditAccounts, new NetWorthCalculator(), snapshots);

        return (service, accounts, creditAccounts, snapshots);
    }

    [Fact]
    public async Task RecordSnapshotAsync_NoExistingRow_CreatesANewSnapshotPerCurrency()
    {
        var (service, accounts, creditAccounts, snapshots) = CreateSut();
        accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        creditAccounts.Add(new CreditCard("Visa", CurrencyCode.USD, Guid.NewGuid(), creditLimit: 1000m,
            statementCutOffDay: 1, paymentDueDay: 15, openingAmountOwed: 200m));

        var asOfDate = new DateOnly(2026, 9, 13);
        await service.RecordSnapshotAsync(asOfDate);

        var snapshot = Assert.Single(snapshots.All);
        Assert.Equal(CurrencyCode.USD, snapshot.Currency);
        Assert.Equal(asOfDate, snapshot.AsOfDate);
        Assert.Equal(500m, snapshot.TotalAssets);
        Assert.Equal(200m, snapshot.TotalLiabilities);
        Assert.Equal(300m, snapshot.NetWorth);
    }

    [Fact]
    public async Task RecordSnapshotAsync_CalledTwiceWithTheSameDate_UpsertsInPlace_NoDuplicate()
    {
        var (service, accounts, _, snapshots) = CreateSut();
        var account = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));

        var asOfDate = new DateOnly(2026, 9, 13);
        await service.RecordSnapshotAsync(asOfDate);

        account.Credit(100m);
        await service.RecordSnapshotAsync(asOfDate);

        var snapshot = Assert.Single(snapshots.All);
        Assert.Equal(600m, snapshot.TotalAssets);
    }

    [Fact]
    public async Task RecordSnapshotAsync_TwoDifferentDates_CreatesTwoSeparateRowsPerCurrency()
    {
        var (service, accounts, _, snapshots) = CreateSut();
        accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));

        await service.RecordSnapshotAsync(new DateOnly(2026, 9, 13));
        await service.RecordSnapshotAsync(new DateOnly(2026, 9, 14));

        Assert.Equal(2, snapshots.All.Count);
    }

    [Fact]
    public async Task RecordSnapshotAsync_WithPrecomputedSummary_UpsertsWithoutRecomputing()
    {
        // Checkpoint review LOW #9: the overload accepting an already-computed NetWorthSummary must
        // persist exactly what it was given, without re-fetching/recalculating from the repositories —
        // proven here by handing it a summary that does not match what a fresh calculation would produce.
        var (service, accounts, _, snapshots) = CreateSut();
        accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));

        var summary = new NetWorthCalculator().Calculate(
            [new CashAccount("Other Wallet", CurrencyCode.USD, openingBalance: 999m)], []);

        var asOfDate = new DateOnly(2026, 9, 13);
        await service.RecordSnapshotAsync(asOfDate, summary);

        var snapshot = Assert.Single(snapshots.All);
        Assert.Equal(999m, snapshot.TotalAssets);
    }

    [Fact]
    public async Task RecordSnapshotAsync_InactiveAccount_ExcludedFromTotals()
    {
        var (service, accounts, _, snapshots) = CreateSut();
        var active = accounts.Add(new CashAccount("Wallet", CurrencyCode.USD, openingBalance: 500m));
        var inactive = accounts.Add(new CashAccount("Old Wallet", CurrencyCode.USD, openingBalance: 1000m));
        inactive.Deactivate();

        await service.RecordSnapshotAsync(new DateOnly(2026, 9, 13));

        var snapshot = Assert.Single(snapshots.All);
        Assert.Equal(500m, snapshot.TotalAssets);
    }

    private sealed class InMemoryFinancialAccountRepository : IFinancialAccountRepository
    {
        private readonly Dictionary<Guid, FinancialAccount> _accounts = new();

        public FinancialAccount Add(FinancialAccount account)
        {
            _accounts[account.Id] = account;
            return account;
        }

        public Task<FinancialAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_accounts.GetValueOrDefault(id));

        public Task<IReadOnlyList<FinancialAccount>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FinancialAccount>>(_accounts.Values.ToList());

        public Task<IReadOnlyList<FinancialAccount>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FinancialAccount>>(_accounts.Values.Where(a => a.IsActive).ToList());

        public Task AddAsync(FinancialAccount entity, CancellationToken ct = default)
        {
            _accounts[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(FinancialAccount entity) => _accounts.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryCreditAccountRepository : ICreditAccountRepository
    {
        private readonly Dictionary<Guid, CreditAccount> _creditAccounts = new();

        public CreditAccount Add(CreditAccount account)
        {
            _creditAccounts[account.Id] = account;
            return account;
        }

        public Task<CreditAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_creditAccounts.GetValueOrDefault(id));

        public Task<IReadOnlyList<CreditAccount>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditAccount>>(_creditAccounts.Values.ToList());

        public Task<IReadOnlyList<CreditAccount>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CreditAccount>>(_creditAccounts.Values.Where(a => a.IsActive).ToList());

        public Task AddAsync(CreditAccount entity, CancellationToken ct = default)
        {
            _creditAccounts[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(CreditAccount entity) => _creditAccounts.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryNetWorthSnapshotRepository : INetWorthSnapshotRepository
    {
        private readonly Dictionary<Guid, NetWorthSnapshot> _snapshots = new();

        public IReadOnlyList<NetWorthSnapshot> All => _snapshots.Values.ToList();

        public Task<NetWorthSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_snapshots.GetValueOrDefault(id));

        public Task<IReadOnlyList<NetWorthSnapshot>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NetWorthSnapshot>>(_snapshots.Values.ToList());

        public Task<NetWorthSnapshot?> GetByCurrencyAndDateAsync(CurrencyCode currency, DateOnly asOfDate, CancellationToken ct = default) =>
            Task.FromResult(_snapshots.Values.FirstOrDefault(s => s.Currency == currency && s.AsOfDate == asOfDate));

        public Task<IReadOnlyList<NetWorthSnapshot>> GetForCurrencyAsync(CurrencyCode currency, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NetWorthSnapshot>>(_snapshots.Values
                .Where(s => s.Currency == currency)
                .OrderBy(s => s.AsOfDate)
                .ToList());

        public Task AddAsync(NetWorthSnapshot entity, CancellationToken ct = default)
        {
            _snapshots[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(NetWorthSnapshot entity) => _snapshots.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
