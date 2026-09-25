using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// "What would I have left by <c>horizonEnd</c>": the real surplus through that date plus recurring
/// income expected by then. Deliberately a separate figure from <see cref="RealSurplus"/> and never
/// folded into it — expected income is a projection, not available balance (same rule as an expected
/// reimbursement, CLAUDE.md), so the conservative real-surplus number must stay income-free.
/// </summary>
public sealed record CashFlowProjection(
    CurrencyCode Currency,
    decimal AvailableBalance,
    decimal ExpectedIncome,
    decimal UpcomingExpenses,
    decimal DebtPayments,
    decimal Projected)
{
    public static CashFlowProjection From(RealSurplus surplus, decimal expectedIncome) =>
        new(
            surplus.Currency,
            surplus.AvailableBalance,
            expectedIncome,
            surplus.UpcomingExpenses,
            surplus.DebtPayments,
            surplus.Amount + expectedIncome);
}

public static class CashFlowProjectionCalculator
{
    /// <summary>
    /// Recurring income not yet confirmed that falls on or before <paramref name="horizonEnd"/>, per
    /// destination-account currency. Counts every occurrence in the window (a semi-monthly salary can
    /// land twice in a month), including overdue unconfirmed ones — until confirmed, that money
    /// hasn't reached the account. Occurrences whose account currency can't be resolved are skipped,
    /// never bucketed under a wrong currency.
    /// </summary>
    public static IReadOnlyDictionary<CurrencyCode, decimal> ExpectedIncomeThrough(
        IEnumerable<RecurringIncome> recurringIncomes,
        DateOnly horizonEnd,
        IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies)
    {
        var byCurrency = new Dictionary<CurrencyCode, decimal>();
        foreach (var recurringIncome in recurringIncomes)
        {
            var occurrences = recurringIncome.CountOccurrencesThrough(horizonEnd);
            if (occurrences == 0)
                continue;

            if (!accountCurrencies.TryGetValue(recurringIncome.DestinationAccountId, out var currency))
                continue;

            byCurrency[currency] = byCurrency.GetValueOrDefault(currency) + recurringIncome.Amount * occurrences;
        }

        return byCurrency;
    }

    /// <summary>
    /// One projection per currency present in either input, never blended across currencies.
    /// </summary>
    public static IReadOnlyList<CashFlowProjection> Project(
        RealSurplusSummary surplus,
        IReadOnlyDictionary<CurrencyCode, decimal> expectedIncomeByCurrency)
    {
        var currencies = surplus.ByCurrency.Keys.Union(expectedIncomeByCurrency.Keys).OrderBy(c => c);

        return currencies
            .Select(currency => CashFlowProjection.From(
                surplus.ByCurrency.TryGetValue(currency, out var s) ? s : RealSurplus.Calculate(currency, 0m, 0m, 0m),
                expectedIncomeByCurrency.GetValueOrDefault(currency)))
            .ToList();
    }
}
