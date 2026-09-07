namespace TrackTraceMoney.Domain.Transactions;

/// <summary>Salary, bonuses, freelance, interest, dividends, etc. (README §10).</summary>
public sealed class Income : Transaction
{
    public Guid DestinationAccountId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? PersonId { get; private set; }

    public override bool CountsAsIncome => true;

    private Income()
    {
    }

    public Income(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        Guid categoryId,
        Guid? personId = null,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        DestinationAccountId = destinationAccountId;
        CategoryId = categoryId;
        PersonId = personId;
    }
}
