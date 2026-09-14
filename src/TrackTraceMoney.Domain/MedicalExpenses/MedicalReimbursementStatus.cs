namespace TrackTraceMoney.Domain.MedicalExpenses;

/// <summary>
/// Tracks a medical <see cref="Transactions.Expense"/>/<see cref="Transactions.CreditCardPurchase"/>'s
/// relationship to insurance (README §25/§26/§27/§29). <see cref="None"/> means no insurance was
/// involved at all. An *expected* reimbursement (<see cref="Pending"/>) must never be treated as
/// available balance until it actually arrives (<see cref="Reimbursed"/>) — see CLAUDE.md's
/// reimbursement rule.
/// </summary>
public enum MedicalReimbursementStatus
{
    None,
    PaidDirectly,
    Pending,
    Reimbursed,
    Rejected
}
