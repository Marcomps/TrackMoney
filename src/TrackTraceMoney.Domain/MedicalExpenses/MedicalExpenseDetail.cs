using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.MedicalExpenses;

/// <summary>
/// Optional insurance detail attached to a medical <see cref="Transactions.Expense"/> or
/// <see cref="Transactions.CreditCardPurchase"/> (README §25/§26/§27/§29). FK'd to the parent
/// transaction by a plain <see cref="Guid"/> — no EF navigation property, no <c>HasOne/WithOne</c> —
/// mirroring <see cref="Accounts.InvestmentValuation"/>'s convention for a "detail" entity hanging off
/// a parent it does not own the lifecycle of.
///
/// The original Expense/CreditCardPurchase amount recorded against the account/card is always the
/// amount the user actually paid out of pocket (net of any insurance that already paid the provider
/// directly) — <see cref="GrossAmount"/> only exists here to remember the pre-insurance total for
/// insurance-involved cases, it is never used to correct the transaction's own <c>Amount</c>.
/// </summary>
public sealed class MedicalExpenseDetail : Entity
{
    public Guid TransactionId { get; private set; }

    public string? InsuranceProvider { get; private set; }

    public decimal? GrossAmount { get; private set; }

    public decimal? InsuranceCoveredAmount { get; private set; }

    public MedicalReimbursementStatus Status { get; private set; }

    public MedicalExpenseDetail(
        Guid transactionId,
        string? insuranceProvider,
        decimal? grossAmount,
        decimal? insuranceCoveredAmount,
        MedicalReimbursementStatus status)
    {
        if (transactionId == Guid.Empty)
            throw new ArgumentException("Transaction id is required.", nameof(transactionId));
        if (status is MedicalReimbursementStatus.Reimbursed or MedicalReimbursementStatus.Rejected)
            throw new ArgumentException(
                "Reimbursed/Rejected are not valid initial statuses — only reachable via MarkReimbursed/MarkRejected.",
                nameof(status));
        if (status == MedicalReimbursementStatus.None && (grossAmount is not null || insuranceCoveredAmount is not null))
            throw new ArgumentException("No insurance amounts are allowed when status is None.");
        if (status is MedicalReimbursementStatus.Pending or MedicalReimbursementStatus.PaidDirectly && grossAmount is not (> 0m))
            throw new ArgumentException("Gross amount must be positive when insurance is involved.", nameof(grossAmount));
        if (insuranceCoveredAmount is > 0m && grossAmount is not null && insuranceCoveredAmount > grossAmount)
            throw new ArgumentOutOfRangeException(nameof(insuranceCoveredAmount), "Covered amount cannot exceed the gross amount.");

        TransactionId = transactionId;
        InsuranceProvider = insuranceProvider;
        GrossAmount = grossAmount;
        InsuranceCoveredAmount = insuranceCoveredAmount;
        Status = status;
    }

    private MedicalExpenseDetail()
    {
    }

    // Unwired this slice — Slice 7 (Reimbursements) will call this atomically alongside constructing
    // its new Reimbursement transaction.
    public void MarkReimbursed(decimal actualAmountReceived)
    {
        if (Status != MedicalReimbursementStatus.Pending)
            throw new InvalidOperationException("Only a Pending medical expense can be marked reimbursed.");
        if (actualAmountReceived <= 0m)
            throw new ArgumentOutOfRangeException(nameof(actualAmountReceived), "Amount received must be positive.");
        if (GrossAmount is not null && actualAmountReceived > GrossAmount)
            throw new ArgumentOutOfRangeException(nameof(actualAmountReceived), "Amount received cannot exceed the gross amount.");

        InsuranceCoveredAmount = actualAmountReceived;
        Status = MedicalReimbursementStatus.Reimbursed;
    }

    public void MarkRejected()
    {
        if (Status != MedicalReimbursementStatus.Pending)
            throw new InvalidOperationException("Only a Pending medical expense can be marked rejected.");
        Status = MedicalReimbursementStatus.Rejected;
    }
}
