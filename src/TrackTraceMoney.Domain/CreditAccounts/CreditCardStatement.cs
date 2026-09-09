using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.CreditAccounts;

/// <summary>
/// One billing cycle's record for a card (README §15) — the "Latest statement"/"Minimum payment"/
/// "Pay-in-full amount" fields README §14 originally listed on CreditCard itself, relocated here since
/// they're per-cycle values, not floating scalars on the card (CLAUDE.md: these two amounts are
/// distinct, user-entered fields — never derive one from the other or cross-validate them against each
/// other). Cycle dates are immutable once recorded — no UpdateCycle method; only the two amounts can be
/// revised via UpdateAmounts. Which purchases/payments belong to this cycle is always a computed
/// date-range query (see ITransactionRepository.GetByDateRangeAndSpendAccountAsync), never a stored
/// transaction→statement link.
/// </summary>
public sealed class CreditCardStatement : Entity
{
    public Guid CreditAccountId { get; private set; }
    public DateOnly CycleStartDate { get; private set; }
    public DateOnly CycleEndDate { get; private set; }
    public decimal MinimumPayment { get; private set; }
    public decimal PayInFullAmount { get; private set; }

    private CreditCardStatement() { }

    public CreditCardStatement(
        Guid creditAccountId,
        DateOnly cycleStartDate,
        DateOnly cycleEndDate,
        decimal minimumPayment,
        decimal payInFullAmount)
    {
        if (cycleEndDate < cycleStartDate)
            throw new ArgumentException("Cycle end date cannot be before cycle start date.", nameof(cycleEndDate));
        if (minimumPayment < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumPayment), "Minimum payment cannot be negative.");
        if (payInFullAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(payInFullAmount), "Pay-in-full amount cannot be negative.");

        CreditAccountId = creditAccountId;
        CycleStartDate = cycleStartDate;
        CycleEndDate = cycleEndDate;
        MinimumPayment = minimumPayment;
        PayInFullAmount = payInFullAmount;
    }

    public void UpdateAmounts(decimal minimumPayment, decimal payInFullAmount)
    {
        if (minimumPayment < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumPayment), "Minimum payment cannot be negative.");
        if (payInFullAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(payInFullAmount), "Pay-in-full amount cannot be negative.");

        MinimumPayment = minimumPayment;
        PayInFullAmount = payInFullAmount;
    }
}
