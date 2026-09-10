namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// A payment made against a loan's debt (README §19, §46's "Card payment" rule family generalized to
/// loans). Structurally identical to CreditCardPayment — two accounts, no category, no payer/
/// beneficiary — crossing hierarchies: source is a FinancialAccount (asset), target is a Loan (a
/// CreditAccount subtype, liability). Never counts as spend — CountsAsExpense/SpendAccountId/
/// SpendCategoryId all stay at Transaction's un-overridden base defaults, exactly like
/// CreditCardPayment. The target property is named CreditAccountId (not LoanAccountId) deliberately —
/// it plays the same role CreditAccountId already plays on CreditCardPurchase/CreditCardPayment ("the
/// target liability account"), which lets EF's TPH mapping intentionally share that column across all
/// three transaction types rather than adding a fourth differently-named one.
/// </summary>
public sealed class LoanPayment : Transaction
{
    public Guid SourceAccountId { get; private set; }

    public Guid CreditAccountId { get; private set; }

    private LoanPayment()
    {
    }

    public LoanPayment(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid creditAccountId,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        SourceAccountId = sourceAccountId;
        CreditAccountId = creditAccountId;
    }
}
