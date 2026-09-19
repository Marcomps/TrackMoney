using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record AccountListItem(Guid Id, string Name, AccountKind Kind, CurrencyCode Currency, decimal Balance, bool IsActive)
{
    /// <summary>
    /// Drives which <c>SwipeItem</c>s <c>AccountsListPage</c> shows for this row (edit/delete slice
    /// spec §1.3) — Edit/Deactivate/Delete for an active account, Reactivate for an inactive one. A
    /// plain computed property rather than an inverse-bool XAML converter (no such converter exists
    /// in this codebase yet, and one row's worth of bindings doesn't justify introducing one).
    /// </summary>
    public bool IsInactive => !IsActive;

    public static AccountListItem FromDomain(FinancialAccount account) => account switch
    {
        CashAccount => new AccountListItem(account.Id, account.Name, AccountKind.Cash, account.Currency, account.Balance, account.IsActive),
        BankAccount => new AccountListItem(account.Id, account.Name, AccountKind.Bank, account.Currency, account.Balance, account.IsActive),
        SavingsAccount => new AccountListItem(account.Id, account.Name, AccountKind.Savings, account.Currency, account.Balance, account.IsActive),
        _ => throw new NotSupportedException($"Unknown financial account type '{account.GetType().Name}'.")
    };
}
