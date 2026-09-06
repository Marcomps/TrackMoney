using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record AccountListItem(Guid Id, string Name, AccountKind Kind, CurrencyCode Currency, decimal Balance)
{
    public static AccountListItem FromDomain(FinancialAccount account) => account switch
    {
        CashAccount => new AccountListItem(account.Id, account.Name, AccountKind.Cash, account.Currency, account.Balance),
        BankAccount => new AccountListItem(account.Id, account.Name, AccountKind.Bank, account.Currency, account.Balance),
        SavingsAccount => new AccountListItem(account.Id, account.Name, AccountKind.Savings, account.Currency, account.Balance),
        _ => throw new NotSupportedException($"Unknown financial account type '{account.GetType().Name}'.")
    };
}
