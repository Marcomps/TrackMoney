using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.Enums;

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

    /// <summary>Fires when a matured term deposit was automatically rolled into a new one (README §22
    /// AutoRenewal, wired via <c>ITermDepositRenewalService</c>).</summary>
    Task NotifyTermDepositRenewedAsync(
        string institution,
        decimal renewedAmount,
        CurrencyCode currency,
        DateOnly newMaturityDate,
        CancellationToken ct = default);
}
