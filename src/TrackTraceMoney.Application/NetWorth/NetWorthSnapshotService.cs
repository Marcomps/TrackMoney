using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.NetWorth;

namespace TrackTraceMoney.Application.NetWorth;

/// <summary>
/// Extends Dashboard Tile 1's existing active-only filtering to <see cref="Domain.CreditAccounts.CreditAccount"/>
/// for the first time (README §24) — net worth is computed from currently active accounts only, then
/// persisted as one <see cref="NetWorthSnapshot"/> row per currency, upserting in place when a row
/// already exists for the same (Currency, AsOfDate) pair so repeated Dashboard loads on the same day
/// never create duplicates.
/// </summary>
public sealed class NetWorthSnapshotService : INetWorthSnapshotService
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly ICreditAccountRepository _creditAccountRepository;
    private readonly INetWorthCalculator _netWorthCalculator;
    private readonly INetWorthSnapshotRepository _snapshotRepository;

    public NetWorthSnapshotService(
        IFinancialAccountRepository financialAccountRepository,
        ICreditAccountRepository creditAccountRepository,
        INetWorthCalculator netWorthCalculator,
        INetWorthSnapshotRepository snapshotRepository)
    {
        _financialAccountRepository = financialAccountRepository;
        _creditAccountRepository = creditAccountRepository;
        _netWorthCalculator = netWorthCalculator;
        _snapshotRepository = snapshotRepository;
    }

    public async Task RecordSnapshotAsync(DateOnly asOfDate, CancellationToken ct = default)
    {
        var financialAccounts = await _financialAccountRepository.GetActiveAsync(ct);
        var creditAccounts = await _creditAccountRepository.GetActiveAsync(ct);

        var summary = _netWorthCalculator.Calculate(financialAccounts, creditAccounts);

        await RecordSnapshotAsync(asOfDate, summary, ct);
    }

    public async Task RecordSnapshotAsync(DateOnly asOfDate, NetWorthSummary summary, CancellationToken ct = default)
    {
        foreach (var byCurrency in summary.ByCurrency.Values)
        {
            var existing = await _snapshotRepository.GetByCurrencyAndDateAsync(byCurrency.Currency, asOfDate, ct);
            if (existing is not null)
                existing.UpdateTotals(byCurrency.TotalAssets, byCurrency.TotalLiabilities);
            else
                await _snapshotRepository.AddAsync(
                    new NetWorthSnapshot(byCurrency.Currency, asOfDate, byCurrency.TotalAssets, byCurrency.TotalLiabilities), ct);
        }

        await _snapshotRepository.SaveChangesAsync(ct);
    }
}
