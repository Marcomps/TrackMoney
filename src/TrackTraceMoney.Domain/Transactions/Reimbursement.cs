namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// Money actually received back for a medical expense already recorded via <see cref="Expense"/>/
/// <see cref="CreditCardPurchase"/> plus a linked <see cref="MedicalExpenses.MedicalExpenseDetail"/>
/// (README §27/§28, Phase 3 slice 7). This is an income/recovery entry linked to the original expense —
/// it never mutates or deletes the original expense record (CLAUDE.md's reimbursement rule). Counts as
/// income (money genuinely arriving into a tracked account), same precedent as <see cref="InterestIncome"/>.
/// An *expected* reimbursement (<see cref="MedicalExpenses.MedicalReimbursementStatus.Pending"/>) is
/// never available balance until this transaction is actually recorded.
/// </summary>
public sealed class Reimbursement : Transaction
{
    public Guid DestinationAccountId { get; private set; }

    public Guid LinkedTransactionId { get; private set; }

    public override bool CountsAsIncome => true;

    public override Guid? IncomeAccountId => DestinationAccountId;

    private Reimbursement()
    {
    }

    public Reimbursement(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        Guid linkedTransactionId,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        if (linkedTransactionId == Guid.Empty)
            throw new ArgumentException("Linked transaction id is required.", nameof(linkedTransactionId));

        DestinationAccountId = destinationAccountId;
        LinkedTransactionId = linkedTransactionId;
    }
}
