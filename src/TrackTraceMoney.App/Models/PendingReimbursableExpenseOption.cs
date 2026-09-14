namespace TrackTraceMoney.App.Models;

/// <summary>
/// A picker-friendly (transaction id, label) pair for <c>AddTransactionViewModel</c>'s Reimbursement
/// block (README §27/§28, Phase 3 slice 7) — one entry per transaction whose linked
/// <see cref="TrackTraceMoney.Domain.MedicalExpenses.MedicalExpenseDetail"/> is currently
/// <see cref="TrackTraceMoney.Domain.MedicalExpenses.MedicalReimbursementStatus.Pending"/>.
/// </summary>
public sealed record PendingReimbursableExpenseOption(Guid TransactionId, string Label)
{
    public override string ToString() => Label;
}
