using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.RecurringExpenses;

/// <summary>
/// A recurring expense definition (e.g. "Netflix", "Rent") that periodically produces a due
/// occurrence the user confirms, posting a real <c>Expense</c> (or, when <see cref="IsCreditCardBacked"/>,
/// a <c>CreditCardPurchase</c>) transaction. Maps onto an existing <c>Category</c> — this entity does
/// not introduce its own category taxonomy.
/// </summary>
public sealed class RecurringExpense : Entity
{
    public string Name { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>
    /// The <c>FinancialAccount</c> this occurrence is paid from. Null when <see cref="IsCreditCardBacked"/>
    /// is true (see <see cref="CreditAccountId"/> instead) — exactly one of the two is ever set.
    /// </summary>
    public Guid? AccountId { get; private set; }

    /// <summary>
    /// The <c>CreditCard</c> this occurrence is charged to. Null unless created via
    /// <see cref="ForCreditCard"/> — exactly one of this and <see cref="AccountId"/> is ever set.
    /// </summary>
    public Guid? CreditAccountId { get; private set; }

    /// <summary>
    /// True when this recurring expense is charged to a credit card (posts a
    /// <c>CreditCardPurchase</c> on confirm) rather than debited from a <c>FinancialAccount</c>
    /// (posts an <c>Expense</c>).
    /// </summary>
    public bool IsCreditCardBacked => CreditAccountId is not null;

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
        : this(name, amount, categoryId, accountId, null, frequency, startDate, endDate)
    {
    }

    /// <summary>
    /// Creates a recurring expense charged to a credit card instead of debited from a
    /// <c>FinancialAccount</c> — confirming its due occurrence posts a <c>CreditCardPurchase</c>
    /// (increasing the card's debt) instead of an <c>Expense</c>. A named factory rather than a second
    /// constructor overload: <c>RecurringExpense(string, decimal, Guid, Guid, ...)</c> for the
    /// FinancialAccount case and a hypothetical credit-card overload would share an identical
    /// parameter-type signature (both take a trailing <see cref="Guid"/>), which is uncompilable as
    /// overloads.
    /// </summary>
    public static RecurringExpense ForCreditCard(
        string name,
        decimal amount,
        Guid categoryId,
        Guid creditAccountId,
        RecurringExpenseFrequency frequency,
        DateOnly startDate,
        DateOnly? endDate) =>
        new(name, amount, categoryId, null, creditAccountId, frequency, startDate, endDate);

    private RecurringExpense(
        string name,
        decimal amount,
        Guid categoryId,
        Guid? accountId,
        Guid? creditAccountId,
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

        // Defense-in-depth/documentation: both public entry points (the FinancialAccount constructor,
        // ForCreditCard) already satisfy this XOR by construction — unreachable through the public API,
        // but guards against a future third caller silently leaving both/neither set.
        if (accountId is null == creditAccountId is null)
            throw new ArgumentException("Exactly one of accountId or creditAccountId must be set.", nameof(accountId));

        Name = name.Trim();
        Amount = amount;
        CategoryId = categoryId;
        AccountId = accountId;
        CreditAccountId = creditAccountId;
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
