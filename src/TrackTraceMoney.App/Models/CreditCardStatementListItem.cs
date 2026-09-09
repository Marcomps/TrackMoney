using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in a credit card's statement history (README §15).</summary>
public sealed record CreditCardStatementListItem(
    Guid Id,
    DateOnly CycleStartDate,
    DateOnly CycleEndDate,
    decimal MinimumPayment,
    decimal PayInFullAmount,
    DateOnly DueDate,
    decimal PurchasesThisCycle)
{
    public static CreditCardStatementListItem FromDomain(CreditCardStatement statement, DateOnly dueDate, decimal purchasesThisCycle) =>
        new(
            statement.Id,
            statement.CycleStartDate,
            statement.CycleEndDate,
            statement.MinimumPayment,
            statement.PayInFullAmount,
            dueDate,
            purchasesThisCycle);
}
