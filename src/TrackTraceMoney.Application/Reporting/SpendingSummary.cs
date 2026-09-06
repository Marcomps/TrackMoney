namespace TrackTraceMoney.Application.Reporting;

public sealed class SpendingSummary
{
    public decimal TotalSpent { get; init; }

    public IReadOnlyDictionary<Guid, decimal> SpentByCategory { get; init; } = new Dictionary<Guid, decimal>();
}
