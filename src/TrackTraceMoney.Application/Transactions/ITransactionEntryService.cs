namespace TrackTraceMoney.Application.Transactions;

/// <summary>
/// Coordinates recording a transaction with the balance mutation it implies on the affected
/// account(s) — these are two aggregates that must change together (README §9), which belongs
/// in the Application layer rather than Domain.
/// </summary>
public interface ITransactionEntryService
{
    Task RecordExpenseAsync(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    Task RecordIncomeAsync(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        Guid categoryId,
        Guid? personId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    Task RecordTransferAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Records a purchase made on a credit card (README §11 "Payment method" = Credit card). This is
    /// the expense — it increases the card's <see cref="Domain.CreditAccounts.CreditAccount.AmountOwed"/>
    /// (debt) and counts as spend. The later payment of that debt is a separate, not-yet-built
    /// <c>CreditCardPayment</c> transaction that reduces a bank account's balance and the card's debt —
    /// it must never be recorded as a second expense (CLAUDE.md's #1 correctness risk).
    /// </summary>
    Task RecordCreditCardPurchaseAsync(
        DateOnly date,
        decimal amount,
        Guid creditAccountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Records a payment against a credit card's debt (README §16, §46). Decreases the source
    /// FinancialAccount's balance and the CreditAccount's AmountOwed. Never counts as spend — the
    /// original CreditCardPurchase was the expense; this is only debt settlement. Do not wire budget/
    /// ISpendingCalculator machinery onto this method (CLAUDE.md's #1 correctness risk, inverse direction).
    /// </summary>
    Task RecordCreditCardPaymentAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid creditAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default);
}
