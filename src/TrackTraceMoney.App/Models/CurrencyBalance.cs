using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>
/// Total available balance for a single currency (README §30 dashboard). Accounts in different
/// currencies are never summed into one number — one line per currency present is shown instead.
/// </summary>
public sealed record CurrencyBalance(CurrencyCode Currency, decimal Total);
