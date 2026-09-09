namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// A spend made on a credit card (README §11, §16, §47 Phase 2). Mirrors <see cref="Expense"/>
/// exactly, except it points at a <see cref="Domain.CreditAccounts.CreditAccount"/> instead of a
/// <see cref="Domain.Accounts.FinancialAccount"/> — the purchase is the expense and increases card
/// debt; the later payment (a future <c>CreditCardPayment</c> slice) reduces bank balance and debt,
/// and is never itself counted as spend (CLAUDE.md's #1 correctness risk in this domain).
/// </summary>
public sealed class CreditCardPurchase : Transaction
{
    public Guid CreditAccountId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? BeneficiaryPersonId { get; private set; }

    public Guid? PayerPersonId { get; private set; }

    public override bool CountsAsExpense => true;

    public override Guid? SpendCategoryId => CategoryId;

    public override Guid? SpendAccountId => CreditAccountId;

    private CreditCardPurchase()
    {
    }

    public CreditCardPurchase(
        DateOnly date,
        decimal amount,
        Guid creditAccountId,
        Guid categoryId,
        Guid? beneficiaryPersonId = null,
        Guid? payerPersonId = null,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        CreditAccountId = creditAccountId;
        CategoryId = categoryId;
        BeneficiaryPersonId = beneficiaryPersonId;
        PayerPersonId = payerPersonId;
    }
}
