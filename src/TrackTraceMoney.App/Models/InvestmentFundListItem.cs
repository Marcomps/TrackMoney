using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record InvestmentFundListItem(
    Guid Id, string Name, string Institution, CurrencyCode Currency,
    decimal Balance, decimal Gain, decimal? ReturnPercentage)
{
    public static InvestmentFundListItem FromDomain(InvestmentFund fund) => new(
        fund.Id, fund.Name, fund.Institution, fund.Currency,
        fund.Balance, fund.Gain, fund.ReturnPercentage);
}
