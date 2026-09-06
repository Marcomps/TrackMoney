using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

public interface ISpendingCalculator
{
    SpendingSummary Calculate(IEnumerable<Transaction> transactions);
}
