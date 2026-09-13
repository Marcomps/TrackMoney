using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>
/// Computes README §24's net worth formula: sum of asset account balances minus sum of liability
/// balances, per currency, from current balances — never from transaction history (CLAUDE.md).
/// Deliberately sums ALL <see cref="FinancialAccount.Balance"/> regardless of
/// <see cref="FinancialAccount.CountsAsAvailableBalance"/> — that flag only governs Dashboard Tile 1's
/// "available balance" concept; a locked TermDeposit or an InvestmentFund is still real net worth.
/// </summary>
public sealed class NetWorthCalculator : INetWorthCalculator
{
    public NetWorthSummary Calculate(IEnumerable<FinancialAccount> financialAccounts, IEnumerable<CreditAccount> creditAccounts)
    {
        var assetsByCurrency = financialAccounts
            .GroupBy(a => a.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Balance));

        var liabilitiesByCurrency = creditAccounts
            .GroupBy(a => a.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.AmountOwed));

        var currencies = assetsByCurrency.Keys.Union(liabilitiesByCurrency.Keys);

        var byCurrency = currencies.ToDictionary(
            currency => currency,
            currency => new NetWorthByCurrency(
                currency,
                assetsByCurrency.GetValueOrDefault(currency),
                liabilitiesByCurrency.GetValueOrDefault(currency)));

        return new NetWorthSummary { ByCurrency = byCurrency };
    }
}
