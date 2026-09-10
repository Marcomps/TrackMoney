namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// README §18's card health semáforo — deliberately exposes only raw facts (dates/amounts/booleans),
/// never colors or messages; presentation is an App-layer concern, mirroring BudgetStatus's convention.
/// This is a separate concept from BudgetStatus (percent-of-budget) and from
/// PurchasedVsPaidIndicator's 2-state comparison — don't merge any of these.
/// </summary>
public sealed record CreditCardHealthAssessment(
    Guid CreditAccountId,
    CreditCardHealthStatus Status,
    DateOnly? DueDate,
    decimal? MinimumPayment,
    decimal? PayInFullAmount,
    decimal PaymentsMadeThisCycle,
    decimal UtilizationRatio,
    bool IsOverdue);
