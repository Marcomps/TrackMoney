using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// Fires an immediate, on-device notification when a budget crossing happens (README §37, narrowed
/// to the one MVP-era signal that actually exists: <c>BudgetStatus.IsOverBudget</c>). Kept as a
/// display-agnostic primitive — implementations own localization/formatting so Application never
/// takes a dependency on App-layer resx resources (CLAUDE.md's one-way reference-direction rule).
///
/// Contract: implementations must never throw. Any platform failure (permission denied, API error,
/// etc.) must be caught and swallowed internally so a notification failure can never break the
/// transaction save that already succeeded.
/// </summary>
public interface ILocalNotifier
{
    Task NotifyBudgetExceededAsync(
        Category category,
        decimal budgetAmount,
        decimal amountOver,
        CancellationToken ct = default);
}
