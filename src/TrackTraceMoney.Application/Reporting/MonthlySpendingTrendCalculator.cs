using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Reuses <see cref="SpendingCalculator"/>'s exact filtering (README §9.1: only
/// <see cref="Transaction.CountsAsExpense"/> transactions count, resolved via <see cref="Transaction.SpendAccountId"/>
/// so a transfer, credit-card payment, loan payment, or investment move can never inflate the trend), just
/// grouped by <c>(currency, year, month)</c> instead of currency alone.
/// </summary>
public sealed class MonthlySpendingTrendCalculator : IMonthlySpendingTrendCalculator
{
    public MonthlySpendingTrendSummary Calculate(IEnumerable<Transaction> transactions, IReadOnlyDictionary<Guid, CurrencyCode> accountCurrencies)
    {
        var byCurrencyAndMonth = new Dictionary<(CurrencyCode Currency, int Year, int Month), decimal>();

        foreach (var transaction in transactions)
        {
            if (!SpendCurrencyResolver.TryResolve(transaction, accountCurrencies, out var currency))
                continue;

            var key = (currency, transaction.Date.Year, transaction.Date.Month);
            byCurrencyAndMonth[key] = byCurrencyAndMonth.GetValueOrDefault(key) + transaction.Amount;
        }

        return new MonthlySpendingTrendSummary { SpentByCurrencyAndMonth = byCurrencyAndMonth };
    }
}
