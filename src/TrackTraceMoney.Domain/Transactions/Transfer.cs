namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// Moves funds between two accounts (README §13). Never counts as spend — the base class default
/// (<see cref="Transaction.CountsAsExpense"/> = false) is intentionally left un-overridden here.
/// </summary>
public sealed class Transfer : Transaction
{
    public Guid SourceAccountId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    private Transfer()
    {
    }

    public Transfer(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        if (sourceAccountId == destinationAccountId)
            throw new ArgumentException("Source and destination accounts must be different.", nameof(destinationAccountId));

        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
    }
}
