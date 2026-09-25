namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// Month-by-month simulation of the snowball strategy, to show roughly when each debt — and all of them —
/// would be paid off. Each month every unpaid debt gets its minimum; the monthly extra plus the minimums
/// freed up by debts already paid off (the "snowball") go to the smallest remaining balance, spilling over
/// to the next one when it's cleared. Same ranking as <see cref="SnowballPlanner"/>.
/// Deliberately ignores interest and new charges, so it's an optimistic estimate and must be shown as
/// approximate.
/// </summary>
public static class SnowballPayoffEstimator
{
    /// <summary>Simulation cap: beyond this a debt is reported as not paid off (e.g. a zero minimum and no extra).</summary>
    public const int MaxMonths = 600;

    /// <returns>
    /// For each debt with a positive balance, the 1-based month in which it's fully paid, or null when it
    /// isn't paid within <see cref="MaxMonths"/>.
    /// </returns>
    public static IReadOnlyDictionary<Guid, int?> EstimatePayoffMonths(IReadOnlyList<SnowballDebtInput> debts, decimal extraMonthly)
    {
        if (extraMonthly < 0)
            throw new ArgumentOutOfRangeException(nameof(extraMonthly), "Extra cannot be negative.");

        var ordered = debts
            .Where(d => d.AmountOwed > 0m)
            .OrderBy(d => d.AmountOwed)
            .ThenBy(d => d.CreatedAtUtc)
            .ThenBy(d => d.CreditAccountId)
            .ToList();

        var balances = ordered.ToDictionary(d => d.CreditAccountId, d => d.AmountOwed);
        var payoff = ordered.ToDictionary(d => d.CreditAccountId, _ => (int?)null);

        for (var month = 1; month <= MaxMonths && payoff.Values.Any(p => p is null); month++)
        {
            var pool = extraMonthly;

            foreach (var debt in ordered)
            {
                if (payoff[debt.CreditAccountId] is not null)
                {
                    // Paid off earlier: its minimum keeps rolling into the snowball.
                    pool += debt.MinimumPayment;
                    continue;
                }

                var payment = Math.Min(debt.MinimumPayment, balances[debt.CreditAccountId]);
                balances[debt.CreditAccountId] -= payment;
                pool += debt.MinimumPayment - payment; // a minimum larger than the balance left over
            }

            // The pool goes to the smallest remaining balance, then the next, until it runs out.
            while (pool > 0m)
            {
                var target = ordered
                    .Where(d => balances[d.CreditAccountId] > 0m)
                    .OrderBy(d => balances[d.CreditAccountId])
                    .ThenBy(d => d.CreatedAtUtc)
                    .ThenBy(d => d.CreditAccountId)
                    .FirstOrDefault();
                if (target is null)
                    break;

                var applied = Math.Min(pool, balances[target.CreditAccountId]);
                balances[target.CreditAccountId] -= applied;
                pool -= applied;
            }

            foreach (var debt in ordered)
            {
                if (payoff[debt.CreditAccountId] is null && balances[debt.CreditAccountId] <= 0m)
                    payoff[debt.CreditAccountId] = month;
            }
        }

        return payoff;
    }
}
