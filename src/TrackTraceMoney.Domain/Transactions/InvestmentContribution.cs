namespace TrackTraceMoney.Domain.Transactions;

/// <summary>
/// Money moved FROM a funding <see cref="Accounts.FinancialAccount"/> (<see cref="SourceAccountId"/>)
/// INTO an <see cref="Accounts.InvestmentFund"/> (<see cref="DestinationAccountId"/>) — README §45's
/// "📈 Investment contribution" quick action. Structurally identical to <see cref="Transfer"/> (both
/// endpoints are FinancialAccount-hierarchy members, unlike Phase 2's CreditCardPayment/LoanPayment
/// which cross into the CreditAccount hierarchy), so it intentionally SHARES Transfer's
/// SourceAccountId/DestinationAccountId mapped columns rather than adding new ones. Never counts as
/// spend — CountsAsExpense/SpendAccountId/SpendCategoryId all stay at Transaction's un-overridden base
/// defaults, exactly like Transfer.
/// </summary>
public sealed class InvestmentContribution : Transaction
{
    public Guid SourceAccountId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    private InvestmentContribution()
    {
    }

    public InvestmentContribution(
        DateOnly date,
        decimal amount,
        Guid sourceAccountId,
        Guid destinationAccountId,
        string? description = null,
        string? notes = null)
        : base(date, amount, description, notes)
    {
        if (sourceAccountId == destinationAccountId)
            throw new ArgumentException("Source and destination accounts must be different.", nameof(destinationAccountId));

        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
    }
}
