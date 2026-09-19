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
    /// Records a medical <see cref="Domain.Transactions.Expense"/> (README §25/§26/§27/§29). Identical
    /// balance mutation and budget-crossing notification behavior to <see cref="RecordExpenseAsync"/> —
    /// a medical expense still counts as spend exactly like any other — plus it additionally persists a
    /// <see cref="Domain.MedicalExpenses.MedicalExpenseDetail"/> linked to the new expense.
    /// </summary>
    Task RecordMedicalExpenseAsync(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        MedicalInsuranceInput medicalInfo,
        CancellationToken ct = default);

    /// <summary>
    /// Records a medical <see cref="Domain.Transactions.CreditCardPurchase"/> (README §25/§26/§27/§29).
    /// Identical behavior to <see cref="RecordCreditCardPurchaseAsync"/> (including its
    /// <c>is not CreditCard</c> guard) plus a linked <see cref="Domain.MedicalExpenses.MedicalExpenseDetail"/>.
    /// </summary>
    Task RecordMedicalCreditCardPurchaseAsync(
        DateOnly date,
        decimal amount,
        Guid creditAccountId,
        Guid categoryId,
        Guid? payerPersonId,
        Guid? beneficiaryPersonId,
        string? description,
        string? notes,
        MedicalInsuranceInput medicalInfo,
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

    /// <summary>
    /// Records a payment against a loan's debt (README §19, §46's "Card payment" rule family, generalized
    /// to loans). Decreases the source FinancialAccount's balance and the Loan's AmountOwed, and advances
    /// the loan's forward-looking schedule (NextPaymentDate/RequiredPayment) to the caller-supplied values.
    /// Never counts as spend — do not wire budget/ISpendingCalculator/ILocalNotifier machinery onto this
    /// method (CLAUDE.md's #1 correctness risk, inverse direction, same as RecordCreditCardPaymentAsync).
    /// </summary>
    Task RecordLoanPaymentAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid loanAccountId,
        DateOnly nextPaymentDate,
        decimal requiredPayment,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Records money moved from a funding <see cref="Domain.Accounts.FinancialAccount"/> into an
    /// <see cref="Domain.Accounts.InvestmentFund"/> (README §45's "📈 Investment contribution" quick
    /// action). Debits the funding account, credits the fund's balance, and updates the fund's running
    /// <see cref="Domain.Accounts.InvestmentFund.Contributions"/> total. Never counts as spend.
    /// </summary>
    Task RecordInvestmentContributionAsync(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid investmentFundId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Records money moved from an <see cref="Domain.Accounts.InvestmentFund"/> into a receiving
    /// <see cref="Domain.Accounts.FinancialAccount"/> (README §45's "💰 Investment withdrawal" quick
    /// action). Debits the fund's balance (allowed to go negative — no overdraft guard, mirroring
    /// <see cref="Domain.Accounts.InvestmentFund.RecordWithdrawal"/>'s deliberately lax convention),
    /// credits the receiving account, and updates the fund's running
    /// <see cref="Domain.Accounts.InvestmentFund.Withdrawals"/> total. Never counts as spend.
    /// </summary>
    Task RecordInvestmentWithdrawalAsync(
        DateOnly date,
        decimal amount,
        Guid investmentFundId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Records interest actually credited to a <see cref="Domain.Accounts.TermDeposit"/> (README §22,
    /// Phase 3 slice 4). Credits the term deposit's balance and updates its running
    /// <see cref="Domain.Accounts.TermDeposit.InterestReceived"/> total. Counts as income (unlike
    /// Transfer/InvestmentContribution) since the money is new, not moved between the user's own
    /// tracked accounts. No cross-currency guard needed — only one account is involved.
    /// </summary>
    Task RecordInterestIncomeAsync(
        DateOnly date,
        decimal amount,
        Guid termDepositId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Records money actually received back for a medical expense (README §27/§28, Phase 3 slice 7).
    /// Only supported when the linked transaction has a <see cref="Domain.MedicalExpenses.MedicalExpenseDetail"/>
    /// currently <see cref="Domain.MedicalExpenses.MedicalReimbursementStatus.Pending"/> — flips it to
    /// <see cref="Domain.MedicalExpenses.MedicalReimbursementStatus.Reimbursed"/> with the actual amount
    /// received (which may differ from the original estimate) and credits the destination account. Counts
    /// as income, never as spend — the original expense already counted as spend and is never mutated here.
    /// </summary>
    Task RecordMedicalReimbursementAsync(
        DateOnly date,
        decimal actualAmountReceived,
        Guid linkedTransactionId,
        Guid destinationAccountId,
        string? description,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a pending medical reimbursement as rejected (README §27/§28) — no money moves and no new
    /// <see cref="Domain.Transactions.Transaction"/> is created, only the linked
    /// <see cref="Domain.MedicalExpenses.MedicalExpenseDetail"/>'s status changes.
    /// </summary>
    Task RejectMedicalReimbursementAsync(Guid linkedTransactionId, CancellationToken ct = default);

    /// <summary>
    /// Reverses a previously-recorded <see cref="Domain.Transactions.Expense"/> (edit/delete slice spec
    /// §4.1) — the exact inverse of <see cref="RecordExpenseAsync"/>'s <c>account.Debit(amount)</c>.
    /// Fetches the transaction and its account fresh from their repositories (never trusts caller/UI
    /// state), then removes the transaction. Used standalone for Delete, and as the first half of Edit
    /// (immediately followed by a fresh <see cref="RecordExpenseAsync"/> call with the edited values).
    /// No overdraft guard — <see cref="Domain.Accounts.FinancialAccount.Debit"/>/<c>Credit</c> have none
    /// today either, so reversal can't newly fail where the original posting couldn't.
    /// </summary>
    Task ReverseExpenseAsync(Guid transactionId, CancellationToken ct = default);

    /// <summary>
    /// Reverses a previously-recorded <see cref="Domain.Transactions.Income"/> — the exact inverse of
    /// <see cref="RecordIncomeAsync"/>'s <c>destinationAccount.Credit(amount)</c>. Same fetch-fresh,
    /// remove, save shape as <see cref="ReverseExpenseAsync"/>.
    /// </summary>
    Task ReverseIncomeAsync(Guid transactionId, CancellationToken ct = default);

    /// <summary>
    /// Reverses a previously-recorded <see cref="Domain.Transactions.Transfer"/> — the exact inverse of
    /// <see cref="RecordTransferAsync"/>'s <c>sourceAccount.Debit(amount)</c>/
    /// <c>destinationAccount.Credit(amount)</c> pair. Re-fetches BOTH accounts fresh, mirroring
    /// <see cref="RecordTransferAsync"/>'s own fetch-both-fresh pattern — never trusts a caller-supplied
    /// account object for either side.
    /// </summary>
    Task ReverseTransferAsync(Guid transactionId, CancellationToken ct = default);
}
