using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

public sealed class BankAccount : FinancialAccount
{
    public string? BankName { get; private set; }

    public string? AccountNumberLast4 { get; private set; }

    private BankAccount()
    {
    }

    public BankAccount(
        string name,
        CurrencyCode currency,
        decimal openingBalance = 0m,
        string? bankName = null,
        string? accountNumberLast4 = null,
        string? notes = null)
        : base(name, currency, openingBalance, notes)
    {
        BankName = bankName;
        AccountNumberLast4 = accountNumberLast4;
    }
}
