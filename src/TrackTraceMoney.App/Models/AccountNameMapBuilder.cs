using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Builds the account-id -> display-name lookup used to render transaction labels (see
/// <see cref="TransactionLabelFormatter"/>), merging both account hierarchies (README §8, §47):
/// <see cref="FinancialAccount"/> and <see cref="CreditAccount"/>. Credit card names get the same
/// "💳 " prefix convention used by <c>AddTransactionViewModel.PaymentAccounts</c>. Mirrors
/// <see cref="TrackTraceMoney.Application.Reporting.AccountCurrencyMapBuilder"/>'s shape. A caller
/// building this dictionary from <see cref="FinancialAccount"/> alone would render a card purchase as
/// "? → Category" since the purchase's account id resolves to a <see cref="CreditAccount"/>.
/// </summary>
public static class AccountNameMapBuilder
{
    public static IReadOnlyDictionary<Guid, string> Build(
        IEnumerable<FinancialAccount> financialAccounts,
        IEnumerable<CreditAccount> creditAccounts)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var a in financialAccounts) map[a.Id] = a.Name;
        foreach (var a in creditAccounts) map[a.Id] = $"💳 {a.Name}";
        return map;
    }
}
