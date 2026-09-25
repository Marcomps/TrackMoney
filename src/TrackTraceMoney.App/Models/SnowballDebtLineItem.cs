using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Application.CreditAccounts;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// One debt in the snowball payment order. <see cref="PayoffMonth"/> is the estimated month it's fully
/// paid (null when the current payments never clear it) — see SnowballPayoffEstimator.
/// </summary>
public sealed record SnowballDebtLineItem(
    Guid CreditAccountId,
    int Position,
    string Name,
    decimal AmountOwed,
    decimal MinimumPayment,
    bool IsCurrentTarget,
    decimal SuggestedExtra,
    decimal SuggestedTotalPayment,
    DateOnly? PayoffMonth)
{
    public string DisplayName => IsCurrentTarget ? $"🎯 {Name}" : Name;

    public string OwedText => string.Format(CultureInfo.CurrentCulture, AppResources.SnowballPlan_OwedFormat, AmountOwed);

    public string PaymentText => IsCurrentTarget && SuggestedExtra > 0m
        ? string.Format(CultureInfo.CurrentCulture, AppResources.SnowballPlan_LinePaymentTargetFormat, SuggestedTotalPayment, MinimumPayment, SuggestedExtra)
        : string.Format(CultureInfo.CurrentCulture, AppResources.SnowballPlan_LinePaymentMinimumFormat, SuggestedTotalPayment);

    public string PayoffText => PayoffMonth is { } month
        ? string.Format(CultureInfo.CurrentCulture, AppResources.SnowballPlan_LinePayoffFormat, month)
        : AppResources.SnowballPlan_LinePayoffUnknown;

    public static SnowballDebtLineItem FromDomain(SnowballDebtPlanLine line, int position, DateOnly? payoffMonth) => new(
        line.CreditAccountId,
        position,
        line.Name,
        line.AmountOwed,
        line.MinimumPayment,
        line.IsCurrentTarget,
        line.SuggestedExtra,
        line.SuggestedTotalPayment,
        payoffMonth);
}
