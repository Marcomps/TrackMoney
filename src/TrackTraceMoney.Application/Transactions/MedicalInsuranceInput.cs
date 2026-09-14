namespace TrackTraceMoney.Application.Transactions;

/// <summary>
/// User-entered insurance info for a medical expense/credit card purchase (README §25/§26/§27/§29),
/// passed into <see cref="ITransactionEntryService.RecordMedicalExpenseAsync"/>/
/// <see cref="ITransactionEntryService.RecordMedicalCreditCardPurchaseAsync"/>. Mirrors the UI's two
/// checkboxes: "insurance covers part of this cost" (null <see cref="InsuranceCoveredAmount"/> means
/// no insurance at all) and, when covered, whether the insurer already paid the provider directly or
/// the user is still waiting to be reimbursed.
/// </summary>
public sealed record MedicalInsuranceInput(
    string? InsuranceProvider,
    decimal? InsuranceCoveredAmount,
    bool InsurancePaidProviderDirectly);
