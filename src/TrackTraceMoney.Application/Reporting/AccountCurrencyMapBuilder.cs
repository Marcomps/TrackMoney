using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Builds the account-id -> currency lookup <see cref="ISpendingCalculator"/>/<see cref="IIncomeCalculator"/>
/// need, merging both account hierarchies (README §8, §47): <see cref="FinancialAccount"/> (assets) and
/// <see cref="CreditAccount"/> (liabilities). Every caller building this dictionary from
/// <see cref="FinancialAccount"/> alone silently drops card purchases from spend totals, since
/// <c>CreditCardPurchase.SpendAccountId</c> resolves to a <see cref="CreditAccount"/> id — see
/// CLAUDE.md's #1 correctness risk. A static pure function, not an injected service.
/// </summary>
public static class AccountCurrencyMapBuilder
{
    public static IReadOnlyDictionary<Guid, CurrencyCode> Build(
        IEnumerable<FinancialAccount> financialAccounts,
        IEnumerable<CreditAccount> creditAccounts)
    {
        var map = new Dictionary<Guid, CurrencyCode>();
        foreach (var a in financialAccounts) map[a.Id] = a.Currency;
        foreach (var a in creditAccounts) map[a.Id] = a.Currency;
        return map;
    }
}
