namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// A spend (README §11). Tracks payer vs. beneficiary separately (README §7, §26) so the app can
/// tell "quién pagó" from "para quién fue el gasto".
/// </summary>
public sealed class Expense : Transaction
{
    public Guid AccountId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid? BeneficiaryPersonId { get; private set; }

    public Guid? PayerPersonId { get; private set; }

    public override bool CountsAsExpense => true;

    public override Guid? SpendCategoryId => CategoryId;

    public override Guid? SpendAccountId => AccountId;

    private Expense()
    {
    }

    public Expense(
        DateOnly date,
        decimal amount,
        Guid accountId,
        Guid categoryId,
        Guid? beneficiaryPersonId = null,
        Guid? payerPersonId = null,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        AccountId = accountId;
        CategoryId = categoryId;
        BeneficiaryPersonId = beneficiaryPersonId;
        PayerPersonId = payerPersonId;
    }
}
