using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Domain.Accounts;

/// <summary>
/// Basic savings account with an optional goal (README §21). Term deposits and investment funds
/// (README §22-23) have materially different fields and belong to a later phase — don't extend
/// this type to cover them.
/// </summary>
public sealed class SavingsAccount : FinancialAccount
{
    public decimal? GoalAmount { get; private set; }

    private SavingsAccount()
    {
    }

    public SavingsAccount(string name, CurrencyCode currency, decimal openingBalance = 0m, decimal? goalAmount = null, string? notes = null)
        : base(name, currency, openingBalance, notes)
    {
        SetGoal(goalAmount);
    }

    public void SetGoal(decimal? goalAmount)
    {
        if (goalAmount is < 0)
            throw new ArgumentOutOfRangeException(nameof(goalAmount), "Goal amount cannot be negative.");

        GoalAmount = goalAmount;
    }
}
