using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// Pure function evaluator for README §18's card health semáforo — no wall-clock time or repository
/// access; <paramref name="today"/> (via the <c>Evaluate</c> parameter) is always supplied explicitly
/// so results are fully deterministic and unit-testable.
/// </summary>
public interface ICreditCardHealthEvaluator
{
    CreditCardHealthAssessment Evaluate(
        CreditCard card,
        CreditCardStatement? latestStatement,
        decimal paymentsMadeThisCycle,
        DateOnly today);
}
