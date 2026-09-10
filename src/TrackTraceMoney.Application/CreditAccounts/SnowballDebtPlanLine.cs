namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// One debt's row in a computed SnowballPlan, in snowball order (smallest balance first). Raw facts
/// only, no presentation — mirrors CreditCardHealthAssessment's convention.
/// </summary>
public sealed record SnowballDebtPlanLine(
    Guid CreditAccountId,
    string Name,
    decimal AmountOwed,
    decimal MinimumPayment,
    bool IsCurrentTarget,
    decimal SuggestedExtra,
    decimal SuggestedTotalPayment);
