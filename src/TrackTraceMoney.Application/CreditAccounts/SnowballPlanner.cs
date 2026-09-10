namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// README §20's pure snowball calculator. Ranks debts ascending by <see cref="SnowballDebtInput.AmountOwed"/>
/// (tie-broken by <see cref="SnowballDebtInput.CreatedAtUtc"/> then <see cref="SnowballDebtInput.CreditAccountId"/>
/// for determinism), covers minimums for every debt, then routes 100% of any declared extra to the single
/// smallest-balance debt — capped at that debt's remaining headroom above its own minimum, mirroring
/// CreditAccount.RegisterPayment's invariant that a payment can never exceed what's owed. This is the pure
/// ("simple") snowball variant only — no avalanche/custom ordering, no multi-month cascading schedule.
/// Debts with <see cref="SnowballDebtInput.AmountOwed"/> &lt;= 0 (paid off but still active) are excluded
/// entirely — never ranked, targeted, or shown — so the freed-up minimum/extra retargets to the next
/// smallest debt per README §20.
/// </summary>
public sealed class SnowballPlanner : ISnowballPlanner
{
    public SnowballPlan Plan(IReadOnlyList<SnowballDebtInput> debts, decimal extraAvailable)
    {
        if (extraAvailable < 0)
            throw new ArgumentOutOfRangeException(nameof(extraAvailable), "Extra available cannot be negative.");

        // Nothing deactivates a CreditAccount when it hits a zero balance, so callers (e.g.
        // SnowballPlanViewModel) may still feed in paid-off debts. A zero-and-below-balance debt has
        // nothing left to pay toward, so it must never be picked as target or displayed as a line —
        // exclude it before ranking/target selection and before the empty-input early return.
        var remaining = debts.Where(d => d.AmountOwed > 0m).ToList();

        if (remaining.Count == 0)
            return new SnowballPlan([], 0m, extraAvailable, 0m, extraAvailable, null);

        var sorted = remaining
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
            // MinimumPayment can be stale (sourced from the latest statement/schedule and never
            // recomputed when AmountOwed drops for other reasons), so cap the suggested total at what's
            // actually owed rather than suggesting an overpayment.
            ? new SnowballDebtPlanLine(d.CreditAccountId, d.Name, d.AmountOwed, d.MinimumPayment, true, extraApplied, Math.Min(d.MinimumPayment + extraApplied, d.AmountOwed))
            : new SnowballDebtPlanLine(d.CreditAccountId, d.Name, d.AmountOwed, d.MinimumPayment, false, 0m, d.MinimumPayment))
            .ToList();

        return new SnowballPlan(lines, sorted.Sum(d => d.MinimumPayment), extraAvailable, extraApplied, unallocated, nextTargetId);
    }
}
