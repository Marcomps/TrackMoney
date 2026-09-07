namespace TrackTraceMoney.App.Models;

/// <summary>
/// A picker-friendly (type, label) pair for the History screen's type filter. <see cref="Type"/> is
/// null for the "all types" sentinel entry, mirroring the <see cref="NamedOption"/>
/// <c>Guid.Empty</c>-as-"all" convention used for the other History filters.
/// </summary>
public sealed record TransactionTypeFilterOption(TransactionType? Type, string Label)
{
    public override string ToString() => Label;
}
