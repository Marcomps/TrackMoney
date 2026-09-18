namespace TrackTraceMoney.App.Models;

/// <summary>
/// Shared proportional-bar sizing for every Reports screen (README §40 slice 1). Per the slice spec's
/// Decision 1, this app deliberately hand-rolls bars (styled <c>Border</c>/<c>BoxView</c> rectangles
/// sized proportional to value) instead of adding a charting library — a fixed-pixel track width scaled
/// by each row's amount relative to the largest amount in its own group, computed here in C# (not a XAML
/// value converter) to match this codebase's existing idiom of pre-computing presentation values on the
/// model (see <c>BudgetListItem.FromDomain</c>'s semáforo emoji).
/// </summary>
public static class ReportBarWidth
{
    public const double MaxWidth = 260;

    /// <summary>
    /// A bar's pixel width proportional to <paramref name="amount"/> relative to
    /// <paramref name="maxAmountInGroup"/> — never proportional to a cross-currency/cross-group total,
    /// since callers only ever pass the max within the same currency/group (CLAUDE.md: never blend
    /// currencies).
    /// </summary>
    public static double Compute(decimal amount, decimal maxAmountInGroup) =>
        maxAmountInGroup <= 0 ? 0 : (double)(amount / maxAmountInGroup) * MaxWidth;

    /// <summary>A bar's pixel width from an already-computed ratio (e.g. a budget's percent-used).</summary>
    public static double ComputeFromRatio(double ratio) => Math.Clamp(ratio, 0, 1) * MaxWidth;
}
