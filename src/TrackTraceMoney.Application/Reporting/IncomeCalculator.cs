using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Aggregates income the mirror-image way of <see cref="SpendingCalculator"/>: only transactions
/// flagged via <see cref="Transaction.CountsAsIncome"/> count, so transfers between own accounts
/// (or any future movement that merely relocates money) never inflate "ingresos del período".
/// </summary>
public sealed class IncomeCalculator : IIncomeCalculator
{
    public IncomeSummary Calculate(IEnumerable<Transaction> transactions)
    {
        decimal total = 0m;

        foreach (var transaction in transactions)
        {
            if (!transaction.CountsAsIncome)
                continue;

            total += transaction.Amount;
        }

        return new IncomeSummary { TotalIncome = total };
    }
}
