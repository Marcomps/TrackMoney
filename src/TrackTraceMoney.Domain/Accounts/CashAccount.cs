using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

public sealed class CashAccount : FinancialAccount
{
    private CashAccount()
    {
    }

    public CashAccount(string name, CurrencyCode currency, decimal openingBalance = 0m, string? notes = null)
        : base(name, currency, openingBalance, notes)
    {
    }
}
