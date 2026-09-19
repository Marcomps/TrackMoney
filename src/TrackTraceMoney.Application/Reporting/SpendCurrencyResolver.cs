using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// The single "does this transaction count as spend, and in which currency" check shared by every
/// spend-aggregating calculator in this namespace (<see cref="SpendingCalculator"/>,
/// <see cref="MonthlySpendingTrendCalculator"/>) — extracted after a checkpoint code review found the
/// two had silently drifted into copy-pasted, independently-maintained copies of CLAUDE.md's #1
/// flagged correctness risk (what counts as spend). A future refinement to that rule now only needs
/// changing here.
/// </summary>
internal static class SpendCurrencyResolver
{
    public static bool TryResolve(
        Transaction transaction,
        IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies,
        out CurrencyCode currency)
    {
        currency = default;

        if (!transaction.CountsAsExpense)
            return false;

        // Defensive: shouldn't happen in practice (every expense's account should be in the lookup),
        // but a lookup gap must not crash the whole calculation.
        return transaction.SpendAccountId is { } accountId
            && accountCurrencies.TryGetValue(accountId, out currency);
    }
}
