using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in an investment fund's valuation history (README §23).</summary>
public sealed record InvestmentValuationListItem(Guid Id, DateOnly AsOfDate, decimal Value)
{
    public static InvestmentValuationListItem FromDomain(InvestmentValuation valuation) =>
        new(valuation.Id, valuation.AsOfDate, valuation.Value);
}
