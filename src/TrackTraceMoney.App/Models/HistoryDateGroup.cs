namespace TrackTraceMoney.App.Models;

/// <summary>A day's worth of <see cref="HistoryEntryItem"/>s for the grouped History list (README §39).</summary>
public sealed class HistoryDateGroup : List<HistoryEntryItem>
{
    public string Header { get; }

    public HistoryDateGroup(string header, IEnumerable<HistoryEntryItem> items) : base(items) => Header = header;
}
