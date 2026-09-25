namespace TrackTraceMoney.App.Models;

/// <summary>
/// A recurring income's next payday on the Dashboard. <see cref="CanConfirm"/> shows the "got paid"
/// button (due, or within the early-confirmation window); <see cref="IsEarly"/> means it isn't due yet.
/// </summary>
public sealed record UpcomingIncomeItem(Guid Id, string Name, decimal Amount, DateOnly Date, bool CanConfirm, bool IsEarly);
