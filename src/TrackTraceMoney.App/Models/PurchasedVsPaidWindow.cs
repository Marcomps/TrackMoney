namespace TrackTraceMoney.App.Models;

/// <summary>
/// The comparison windows README §17 offers for the "purchased vs. paid" analysis. "Cycle" always
/// means the card's current in-progress billing cycle — there is no historical-cycle picker in this
/// slice.
/// </summary>
public enum PurchasedVsPaidWindow
{
    Month,
    Cycle,
    ThreeMonths,
    SixMonths,
    Year
}

public sealed record PurchasedVsPaidWindowOption(PurchasedVsPaidWindow Window, string Label);
