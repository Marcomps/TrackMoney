using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Reporting;

/// <summary>Income aggregated per currency — mirrors <see cref="SpendingSummary"/>'s stance.</summary>
public sealed class IncomeSummary
{
    public IReadOnlyDictionary<CurrencyCode, decimal> TotalIncomeByCurrency { get; init; } = new Dictionary<CurrencyCode, decimal>();
}
