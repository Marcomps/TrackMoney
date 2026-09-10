namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// README §18's card health semáforo (4 real states plus a "no data yet" pseudo-state). Deliberately
/// separate from <see cref="TrackTraceMoney.Application.Budgets.BudgetStatus"/> (percent-of-budget) and
/// from the App layer's PurchasedVsPaidIndicator (2-state purchased-vs-paid comparison) — don't merge
/// any of these three concepts.
/// </summary>
public enum CreditCardHealthStatus
{
    NoStatementYet,
    Green,
    Yellow,
    Orange,
    Red
}
