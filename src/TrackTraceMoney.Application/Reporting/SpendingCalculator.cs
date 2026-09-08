using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Aggregates spend the way README §9.1 requires: transfers, income, and any other movement that
/// isn't flagged via <see cref="Transaction.CountsAsExpense"/> are excluded, so a card payment or
/// account transfer can never inflate "gastos del período" — see CLAUDE.md's non-obvious domain
/// rules for why this matters. Also never blends currencies: each transaction is grouped by the
/// currency of the account its spend actually happened through (<see cref="Transaction.SpendAccountId"/>),
/// so a $20 USD expense and a $20 MXN expense in the same category never sum to "40".
/// </summary>
public sealed class SpendingCalculator : ISpendingCalculator
{
    public SpendingSummary Calculate(IEnumerable<Transaction> transactions, IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies)
    {
        var totalByCurrency = new Dictionary<CurrencyCode, decimal>();
        var byCategoryAndCurrency = new Dictionary<(CurrencyCode Currency, Guid CategoryId), decimal>();

        foreach (var transaction in transactions)
        {
            if (!transaction.CountsAsExpense)
                continue;

            if (transaction.SpendAccountId is not { } accountId
                || !accountCurrencies.TryGetValue(accountId, out var currency))
            {
                // Defensive: shouldn't happen in practice (every expense's account should be in the
                // lookup), but a lookup gap must not crash the whole calculation.
                continue;
            }

            totalByCurrency[currency] = totalByCurrency.GetValueOrDefault(currency) + transaction.Amount;

            if (transaction.SpendCategoryId is { } categoryId)
            {
                var key = (currency, categoryId);
                byCategoryAndCurrency[key] = byCategoryAndCurrency.GetValueOrDefault(key) + transaction.Amount;
            }
        }

        return new SpendingSummary { TotalSpentByCurrency = totalByCurrency, SpentByCategoryAndCurrency = byCategoryAndCurrency };
    }
}
