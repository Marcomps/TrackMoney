using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.RecurringExpenses;

/// <summary>
/// A recurring expense definition (e.g. "Netflix", "Rent") that periodically produces a due
/// occurrence the user confirms, posting a real <c>Expense</c> transaction. Maps onto an existing
/// <c>Category</c> — this entity does not introduce its own category taxonomy.
/// </summary>
public sealed class RecurringExpense : Entity
{
    public string Name { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid AccountId { get; private set; }

    public RecurringExpenseFrequency Frequency { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// Domain-internal bookkeeping of the last occurrence the user confirmed — never user-entered.
    /// Null until the first occurrence is confirmed.
    /// </summary>
    public DateOnly? LastConfirmedDate { get; private set; }

    public bool IsActive { get; private set; } = true;

    private RecurringExpense()
    {
    }

    public RecurringExpense(
        string name,
        decimal amount,
        Guid categoryId,
        Guid accountId,
        RecurringExpenseFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recurring expense name cannot be empty.", nameof(name));

        if (amount <= 0)
            throw new ArgumentException("Recurring expense amount must be positive.", nameof(amount));

        if (endDate is not null && endDate.Value < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        Name = name.Trim();
        Amount = amount;
        CategoryId = categoryId;
        AccountId = accountId;
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// The next occurrence date: the start date if nothing has been confirmed yet, otherwise the
    /// last confirmed date advanced by one period.
    /// </summary>
    public DateOnly NextOccurrenceDate =>
        LastConfirmedDate is null ? StartDate : Advance(LastConfirmedDate.Value);

    private DateOnly Advance(DateOnly d) => Frequency switch
    {
        RecurringExpenseFrequency.Weekly => d.AddDays(7),
        RecurringExpenseFrequency.Monthly => d.AddMonths(1),
        RecurringExpenseFrequency.Yearly => d.AddYears(1),
        _ => throw new InvalidOperationException($"Unknown frequency '{Frequency}'.")
    };

    public bool IsDue(DateOnly asOf) =>
        IsActive
        && NextOccurrenceDate <= asOf
        && (EndDate is null || NextOccurrenceDate <= EndDate.Value);

    public void MarkConfirmed(DateOnly occurrenceDate)
    {
        if (occurrenceDate != NextOccurrenceDate)
            throw new InvalidOperationException("Can only confirm the currently-due occurrence.");

        LastConfirmedDate = occurrenceDate;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
