namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// Interest credited to a <see cref="Accounts.TermDeposit"/> (README §22, Phase 3 slice 4) — wires
/// <see cref="Accounts.TermDeposit.RecordInterestReceived"/> to a concrete transaction type. No
/// <c>CategoryId</c>/<c>PersonId</c>: this transaction's meaning is unambiguous from its type alone,
/// same precedent as <see cref="Transfer"/>/<see cref="InvestmentContribution"/>. Counts as income
/// (unlike Transfer/InvestmentContribution) since the money is genuinely new, originating from the
/// institution rather than moved between the user's own tracked accounts.
/// </summary>
public sealed class InterestIncome : Transaction
{
    public Guid DestinationAccountId { get; private set; }

    public override bool CountsAsIncome => true;

    public override Guid? IncomeAccountId => DestinationAccountId;

    private InterestIncome()
    {
    }

    public InterestIncome(
        DateOnly date,
        decimal amount,
        Guid destinationAccountId,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        DestinationAccountId = destinationAccountId;
    }
}
