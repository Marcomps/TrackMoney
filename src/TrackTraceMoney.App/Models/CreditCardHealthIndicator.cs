using TrackTraceMoney.Application.CreditAccounts;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// README §18's 4-state (plus no-data) card health semáforo. Unrelated to
/// PurchasedVsPaidIndicator's 2-state purchased-vs-paid comparison and to BudgetStatus's
/// percent-of-budget concept — don't merge any of these.
/// </summary>
public static class CreditCardHealthIndicator
{
    public static string GetEmoji(CreditCardHealthStatus status) => status switch
    {
        CreditCardHealthStatus.Red => "\U0001F534",           // 🔴
        CreditCardHealthStatus.Orange => "\U0001F7E0",        // 🟠
        CreditCardHealthStatus.Yellow => "\U0001F7E1",        // 🟡
        CreditCardHealthStatus.Green => "\U0001F7E2",         // 🟢
        CreditCardHealthStatus.NoStatementYet => "⚪",    // ⚪ — not one of the four real states
        _ => string.Empty
    };
}
