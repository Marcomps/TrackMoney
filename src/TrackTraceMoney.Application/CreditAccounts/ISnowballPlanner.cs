namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// Pure calculator for README §20's Snowball strategy: minimums first, 100% of any declared extra to
/// the single smallest-balance debt, capped at that debt's remaining headroom. No repository or
/// wall-clock access — every input is supplied by the caller, mirroring ICreditCardHealthEvaluator.
/// </summary>
public interface ISnowballPlanner
{
    SnowballPlan Plan(IReadOnlyList<SnowballDebtInput> debts, decimal extraAvailable);
}
