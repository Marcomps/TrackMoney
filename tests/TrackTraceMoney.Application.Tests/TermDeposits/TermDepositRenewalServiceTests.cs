using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.TermDeposits;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Tests.TermDeposits;

/// <summary>
/// Covers <see cref="TermDepositRenewalService.ProcessMaturedRenewalsAsync"/>: which deposits are
/// eligible, that each renewal (new account + old account's deactivation) commits atomically, that
/// repeated calls are idempotent (a deactivated deposit no longer surfaces via GetActiveAsync, so it's
/// never reconsidered), and that one deposit failing to renew doesn't block the rest of the batch —
/// mirroring RecurringExpenseServiceTests' verification style for its own atomic pair.
/// </summary>
public sealed class TermDepositRenewalServiceTests
{
    private static (TermDepositRenewalService Service, InMemoryFinancialAccountRepository Accounts, FakeUnitOfWork UnitOfWork) CreateSut()
    {
        var accounts = new InMemoryFinancialAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new TermDepositRenewalService(accounts, unitOfWork);
        return (service, accounts, unitOfWork);
    }

    private static TermDeposit CreateMaturedAutoRenewingDeposit(
        decimal balance = 10000m,
        DateOnly? startDate = null,
        DateOnly? maturityDate = null,
        bool autoRenewal = true) =>
        new(
            "12-Month CD",
            CurrencyCode.USD,
            "Bank of Example",
            initialPrincipal: balance,
            openingBalance: balance,
            rate: 0.05m,
            TermDepositRateType.Nominal,
            startDate ?? new DateOnly(2026, 1, 1),
            maturityDate ?? new DateOnly(2027, 1, 1),
            TermDepositInterestFrequency.Monthly,
            isCompounding: true,
            autoRenewal: autoRenewal);

    [Fact]
    public async Task ProcessMaturedRenewalsAsync_RenewsEligibleMaturedDeposit_AndReturnsResult()
    {
        var (service, accounts, _) = CreateSut();
        var deposit = accounts.Add(CreateMaturedAutoRenewingDeposit(maturityDate: new DateOnly(2027, 1, 1)));
        var asOfDate = new DateOnly(2027, 1, 1);

        var results = await service.ProcessMaturedRenewalsAsync(asOfDate);

        var result = Assert.Single(results);
        Assert.Equal(deposit.Id, result.OldTermDepositId);
        Assert.NotEqual(deposit.Id, result.NewTermDepositId);
        Assert.Equal("Bank of Example", result.Institution);
        Assert.Equal(CurrencyCode.USD, result.Currency);
        Assert.Equal(10000m, result.RenewedAmount);
        Assert.Equal(new DateOnly(2028, 1, 1), result.NewMaturityDate);
    }

    [Fact]
    public async Task ProcessMaturedRenewalsAsync_SkipsDepositWithAutoRenewalFalse()
    {
        var (service, accounts, _) = CreateSut();
        accounts.Add(CreateMaturedAutoRenewingDeposit(maturityDate: new DateOnly(2027, 1, 1), autoRenewal: false));

        var results = await service.ProcessMaturedRenewalsAsync(new DateOnly(2027, 1, 1));

        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessMaturedRenewalsAsync_SkipsDepositNotYetMatured()
    {
        var (service, accounts, _) = CreateSut();
        accounts.Add(CreateMaturedAutoRenewingDeposit(maturityDate: new DateOnly(2027, 1, 1)));

        var results = await service.ProcessMaturedRenewalsAsync(new DateOnly(2026, 12, 31));

        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessMaturedRenewalsAsync_SkipsAlreadyDeactivatedDeposit()
    {
        // Proves idempotency across repeated calls: a deposit renewed by a first call is deactivated,
        // so it must not be reconsidered (or renewed again) by a second call.
        var (service, accounts, _) = CreateSut();
        accounts.Add(CreateMaturedAutoRenewingDeposit(maturityDate: new DateOnly(2027, 1, 1)));
        var asOfDate = new DateOnly(2027, 1, 1);

        var firstResults = await service.ProcessMaturedRenewalsAsync(asOfDate);
        var secondResults = await service.ProcessMaturedRenewalsAsync(asOfDate);

        Assert.Single(firstResults);
        Assert.Empty(secondResults);

        // Exactly one renewal exists in the store, not two.
        var allDeposits = (await accounts.GetAllAsync()).OfType<TermDeposit>().ToList();
        Assert.Equal(2, allDeposits.Count); // original (inactive) + the one renewal (active).
        Assert.Single(allDeposits.Where(d => d.IsActive));
    }

    [Fact]
    public async Task ProcessMaturedRenewalsAsync_WhenOneDepositFailsToRenew_StillRenewsOthersInTheSameBatch()
    {
        var (service, accounts, _) = CreateSut();
        var asOfDate = new DateOnly(2027, 1, 1);

        // Unhealthy: matured, auto-renewal enabled, but a zero balance makes RenewAtMaturity throw.
        var unhealthy = CreateMaturedAutoRenewingDeposit(balance: 100m, maturityDate: asOfDate);
        unhealthy.Debit(100m);
        accounts.Add(unhealthy);

        var healthy = CreateMaturedAutoRenewingDeposit(balance: 5000m, maturityDate: asOfDate);
        accounts.Add(healthy);

        var results = await service.ProcessMaturedRenewalsAsync(asOfDate);

        var result = Assert.Single(results);
        Assert.Equal(healthy.Id, result.OldTermDepositId);
        Assert.True(unhealthy.IsActive); // Untouched — its failed renewal must not deactivate it either.
        Assert.False(healthy.IsActive);
    }

    [Fact]
    public async Task ProcessMaturedRenewalsAsync_PersistsBothDeactivationAndNewAccount_Atomically()
    {
        var (service, accounts, _) = CreateSut();
        var deposit = accounts.Add(CreateMaturedAutoRenewingDeposit(maturityDate: new DateOnly(2027, 1, 1)));
        var asOfDate = new DateOnly(2027, 1, 1);

        var results = await service.ProcessMaturedRenewalsAsync(asOfDate);
        var result = Assert.Single(results);

        var allDeposits = (await accounts.GetAllAsync()).OfType<TermDeposit>().ToDictionary(d => d.Id);
        Assert.True(allDeposits.ContainsKey(deposit.Id));
        Assert.False(allDeposits[deposit.Id].IsActive);
        Assert.True(allDeposits.ContainsKey(result.NewTermDepositId));
        Assert.True(allDeposits[result.NewTermDepositId].IsActive);
    }

    private sealed class InMemoryFinancialAccountRepository : IFinancialAccountRepository
    {
        private readonly Dictionary<Guid, FinancialAccount> _accounts = new();

        public TermDeposit Add(TermDeposit account)
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
            Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction());

        private sealed class FakeTransaction : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;

            public IDisposable EnterAmbientScope() => NoopScope.Instance;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            private sealed class NoopScope : IDisposable
            {
                public static readonly NoopScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
