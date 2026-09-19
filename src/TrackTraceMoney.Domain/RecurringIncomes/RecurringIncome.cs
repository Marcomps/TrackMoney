using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.RecurringIncomes;

/// <summary>
/// A recurring income definition (e.g. "Salary", "Rental income") that periodically produces a
/// due occurrence the user confirms, posting a real <c>Income</c> transaction. Maps onto an
/// existing <c>Category</c> — this entity does not introduce its own category taxonomy.
/// Deliberately simpler than <c>RecurringExpense</c>: <c>Income</c> has no credit-card
/// destination concept in this domain (see the recurring-income slice spec's Decision B), so
/// there is a single non-nullable <see cref="DestinationAccountId"/> and no
/// <c>IsCreditCardBacked</c> duality; and, mirroring <c>RecurringExpense</c>'s own precedent of
/// never capturing Person fields (Decision C), there is no <c>PersonId</c> here either.
/// </summary>
public sealed class RecurringIncome : Entity
{
    public string Name { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    public RecurringIncomeFrequency Frequency { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// Domain-internal bookkeeping of the last occurrence the user confirmed — never user-entered.
    /// Null until the first occurrence is confirmed.
    /// </summary>
    public DateOnly? LastConfirmedDate { get; private set; }

    public bool IsActive { get; private set; } = true;

    private RecurringIncome()
    {
    }

    public RecurringIncome(
        string name,
        decimal amount,
        Guid categoryId,
        Guid destinationAccountId,
        RecurringIncomeFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recurring income name cannot be empty.", nameof(name));

        if (amount <= 0)
            throw new ArgumentException("Recurring income amount must be positive.", nameof(amount));

        if (endDate is not null && endDate.Value < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        Name = name.Trim();
        Amount = amount;
        CategoryId = categoryId;
        DestinationAccountId = destinationAccountId;
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
        RecurringIncomeFrequency.Weekly => d.AddDays(7),
        RecurringIncomeFrequency.Biweekly => d.AddDays(14),
        RecurringIncomeFrequency.Monthly => d.AddMonths(1),
        RecurringIncomeFrequency.Yearly => d.AddYears(1),
        _ => throw new InvalidOperationException($"Unknown frequency '{Frequency}'.")
    };

    public bool IsDue(DateOnly asOf) =>
        IsActive
        && NextOccurrenceDate <= asOf
        && (EndDate is null || NextOccurrenceDate <= EndDate.Value);

    /// <summary>
    /// How many occurrences fall on or before <paramref name="horizonEnd"/>, starting from
    /// <see cref="NextOccurrenceDate"/> — not just whether one is due (<see cref="IsDue"/>).
    /// Identical algorithm to <c>RecurringExpense.CountOccurrencesThrough</c>, walking this
    /// entity's own <see cref="Advance"/>.
    /// </summary>
    public int CountOccurrencesThrough(DateOnly horizonEnd)
    {
        if (!IsActive)
            return 0;

        var count = 0;
        var occurrence = NextOccurrenceDate;
        while (occurrence <= horizonEnd && (EndDate is null || occurrence <= EndDate.Value))
        {
            count++;
            occurrence = Advance(occurrence);
        }

        return count;
    }

    public void MarkConfirmed(DateOnly occurrenceDate)
    {
        if (occurrenceDate != NextOccurrenceDate)
            throw new InvalidOperationException("Can only confirm the currently-due occurrence.");

        LastConfirmedDate = occurrenceDate;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Changes the recurring amount going forward (e.g. a salary raise) — never touches any
    /// already-posted <c>Income</c> transaction, <see cref="LastConfirmedDate"/>, or past
    /// occurrences. Mirrors <c>Budget.UpdateAmount(decimal)</c>'s exact shape (single
    /// positive-amount validation, direct field set, no side effects): like
    /// <c>Budget.Amount</c>, <see cref="Amount"/> is a live, ongoing configuration value with no
    /// "as of" dimension, unlike e.g. <c>InvestmentValuation.UpdateValuation</c> which corrects a
    /// value tied to a specific dated snapshot. Prospective-only is a hard decision, not left
    /// open: confirming a future due occurrence after a raise posts the new amount, since
    /// <c>RecurringIncomeService.ConfirmOccurrenceAsync</c> always reads <see cref="Amount"/> at
    /// confirm-time, not at definition-time.
    /// </summary>
    public void UpdateAmount(decimal newAmount)
    {
        if (newAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(newAmount), "Recurring income amount must be positive.");

        Amount = newAmount;
    }
}
