using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Application.Reporting;

public interface IIncomeCalculator
{
    IncomeSummary Calculate(IEnumerable<Transaction> transactions);
}
