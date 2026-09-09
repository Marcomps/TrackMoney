namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// A payment made against a credit card's debt (README §16, §46). Structurally like
/// <see cref="Transfer"/> — two accounts, no category, no payer/beneficiary — but crosses the two
/// account hierarchies: source is a <see cref="Domain.Accounts.FinancialAccount"/> (asset), target is
/// a <see cref="Domain.CreditAccounts.CreditAccount"/> (liability), so it cannot literally reuse
/// Transfer's same-hierarchy source/destination pair. Never counts as spend — the base class default
/// (<see cref="Transaction.CountsAsExpense"/> = false) is intentionally left un-overridden, exactly
/// like Transfer. The purchase (<see cref="CreditCardPurchase"/>) was the expense; this is only debt
/// settlement (CLAUDE.md's #1 correctness risk — do not flip this to true).
/// </summary>
public sealed class CreditCardPayment : Transaction
{
    public Guid SourceAccountId { get; private set; }

    public Guid CreditAccountId { get; private set; }

    private CreditCardPayment()
    {
    }

    public CreditCardPayment(
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
