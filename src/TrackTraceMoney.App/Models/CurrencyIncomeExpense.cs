using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Income / expenses / available for a single currency, this month (README §30 dashboard tile 2).
/// Mirrors <see cref="CurrencyBalance"/>'s stance — accounts (and the transactions that move money
/// through them) in different currencies are never summed into one number.
/// </summary>
public sealed record CurrencyIncomeExpense(CurrencyCode Currency, decimal Income, decimal Expenses)
{
    public decimal Available => Income - Expenses;
}
