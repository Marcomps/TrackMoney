namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// README §20's pure snowball calculator. Ranks debts ascending by <see cref="SnowballDebtInput.AmountOwed"/>
/// (tie-broken by <see cref="SnowballDebtInput.CreatedAtUtc"/> then <see cref="SnowballDebtInput.CreditAccountId"/>
/// for determinism), covers minimums for every debt, then routes 100% of any declared extra to the single
/// smallest-balance debt — capped at that debt's remaining headroom above its own minimum, mirroring
/// CreditAccount.RegisterPayment's invariant that a payment can never exceed what's owed. This is the pure
/// ("simple") snowball variant only — no avalanche/custom ordering, no multi-month cascading schedule.
/// </summary>
public sealed class SnowballPlanner : ISnowballPlanner
{
    public SnowballPlan Plan(IReadOnlyList<SnowballDebtInput> debts, decimal extraAvailable)
    {
        if (extraAvailable < 0)
            throw new ArgumentOutOfRangeException(nameof(extraAvailable), "Extra available cannot be negative.");

        if (debts.Count == 0)
            return new SnowballPlan([], 0m, extraAvailable, 0m, extraAvailable, null);

        var sorted = debts
            .OrderBy(d => d.AmountOwed)
            .ThenBy(d => d.CreatedAtUtc)
            .ThenBy(d => d.CreditAccountId)
            .ToList();

        var target = sorted[0];
        var headroom = Math.Max(0m, target.AmountOwed - target.MinimumPayment);
        var extraApplied = Math.Min(extraAvailable, headroom);
        var unallocated = extraAvailable - extraApplied;
        var nextTargetId = unallocated > 0m && sorted.Count > 1 ? sorted[1].CreditAccountId : (Guid?)null;

        var lines = sorted.Select((d, i) => i == 0
            ? new SnowballDebtPlanLine(d.CreditAccountId, d.Name, d.AmountOwed, d.MinimumPayment, true, extraApplied, d.MinimumPayment + extraApplied)
            : new SnowballDebtPlanLine(d.CreditAccountId, d.Name, d.AmountOwed, d.MinimumPayment, false, 0m, d.MinimumPayment))
            .ToList();

        return new SnowballPlan(lines, debts.Sum(d => d.MinimumPayment), extraAvailable, extraApplied, unallocated, nextTargetId);
    }
}
