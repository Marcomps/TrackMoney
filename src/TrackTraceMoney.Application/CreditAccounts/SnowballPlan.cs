namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// README §20's snowball plan for one currency group of debts. Advisory only — nothing here posts a
/// transaction. Lines is already in snowball order (ascending AmountOwed); Lines[0] is the current
/// target when Lines is non-empty.
/// </summary>
public sealed record SnowballPlan(
    IReadOnlyList<SnowballDebtPlanLine> Lines,
    decimal TotalMinimums,
    decimal ExtraAvailable,
    decimal ExtraAppliedToTarget,
    decimal UnallocatedExtra,
    Guid? NextTargetAfterCurrentId)
{
    public bool IsEmpty => Lines.Count == 0;
}
