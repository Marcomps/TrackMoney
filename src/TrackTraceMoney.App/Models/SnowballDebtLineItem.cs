using TrackTraceMoney.Application.CreditAccounts;

namespace TrackTraceMoney.App.Models;

public sealed record SnowballDebtLineItem(
    Guid CreditAccountId,
    string Name,
    string DisplayName,
    decimal AmountOwed,
    decimal MinimumPayment,
    bool IsCurrentTarget,
    decimal SuggestedExtra,
    decimal SuggestedTotalPayment)
{
    public static SnowballDebtLineItem FromDomain(SnowballDebtPlanLine line) => new(
        line.CreditAccountId,
        line.Name,
        line.IsCurrentTarget ? $"⭐ {line.Name}" : line.Name,
        line.AmountOwed,
        line.MinimumPayment,
        line.IsCurrentTarget,
        line.SuggestedExtra,
        line.SuggestedTotalPayment);
}
