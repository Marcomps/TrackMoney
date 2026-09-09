namespace TrackTraceMoney.App.Models;

/// <summary>
/// README §17's two-state indicator for the "purchased vs. paid" comparison — deliberately just
/// 🟠/🟢, unlike the future §18 4-state card health "semáforo". Don't merge these two concepts.
/// </summary>
public static class PurchasedVsPaidIndicator
{
    public static string GetEmoji(bool isSpendingMoreThanPaying) =>
        isSpendingMoreThanPaying ? "\U0001F7E0" /* 🟠 */ : "\U0001F7E2" /* 🟢 */;
}
