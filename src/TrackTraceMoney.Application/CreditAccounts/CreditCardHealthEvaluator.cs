using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// README §18's card health semáforo, evaluated top-down with the first matching rule winning
/// (exact precedence supplied by the product-owner scoping pass — do not reorder):
/// <list type="number">
/// <item>Red: overdue AND the minimum payment itself wasn't met (NOT the pay-in-full amount — carrying
/// a revolving balance past the due date after paying at least the minimum is normal and must fall
/// through to Orange, not Red). A recorded <c>$0</c> minimum is handled deliberately: it only counts as
/// "met" when nothing is owed, so an overdue card with a <c>$0</c> minimum and a real balance still
/// unpaid is still Red, not a false Green.</item>
/// <item>Orange: a partial payment was made (paid at least the minimum but not the full pay-in-full
/// amount) OR utilization is at/above 80% — either condition alone is enough, checked before Yellow's
/// due-date proximity so high utilization or a partial payment always outranks "due soon".</item>
/// <item>Yellow: not paid in full, not overdue, and the due date is within 7 days.</item>
/// <item>Green: otherwise.</item>
/// </list>
/// A missing <see cref="CreditCardStatement"/> short-circuits all of the above into the
/// <see cref="CreditCardHealthStatus.NoStatementYet"/> pseudo-state.
/// </summary>
public sealed class CreditCardHealthEvaluator : ICreditCardHealthEvaluator
{
    public CreditCardHealthAssessment Evaluate(
        CreditCard card,
        CreditCardStatement? latestStatement,
        decimal paymentsMadeThisCycle,
        DateOnly today)
    {
        var utilizationRatio = card.AmountOwed / card.CreditLimit;

        if (latestStatement is null)
        {
            return new CreditCardHealthAssessment(
                card.Id,
                CreditCardHealthStatus.NoStatementYet,
                DueDate: null,
                MinimumPayment: null,
                PayInFullAmount: null,
                PaymentsMadeThisCycle: 0m,
                UtilizationRatio: utilizationRatio,
                IsOverdue: false);
        }

        var dueDate = card.GetPaymentDueDateForCycleEndingOn(latestStatement.CycleEndDate);
        var isOverdue = today > dueDate;
        // A $0 minimum must not get a free pass: "paymentsMadeThisCycle >= 0" is trivially true, which
        // would otherwise let a fully unpaid, overdue balance read as "minimum met". When the recorded
        // minimum is $0, only treat it as met if there's genuinely nothing left to owe.
        var paidAtLeastMinimum = latestStatement.MinimumPayment > 0
            ? paymentsMadeThisCycle >= latestStatement.MinimumPayment
            : card.AmountOwed <= 0;
        var paidInFull = paymentsMadeThisCycle >= latestStatement.PayInFullAmount;

        CreditCardHealthStatus status;
        if (isOverdue && !paidAtLeastMinimum)
        {
            status = CreditCardHealthStatus.Red;
        }
        else if ((paymentsMadeThisCycle > 0 && paidAtLeastMinimum && !paidInFull) || utilizationRatio >= 0.80m)
        {
            status = CreditCardHealthStatus.Orange;
        }
        else if (!paidInFull && !isOverdue && (dueDate.DayNumber - today.DayNumber) <= 7)
        {
            status = CreditCardHealthStatus.Yellow;
        }
        else
        {
            status = CreditCardHealthStatus.Green;
        }

        return new CreditCardHealthAssessment(
            card.Id,
            status,
            dueDate,
            latestStatement.MinimumPayment,
            latestStatement.PayInFullAmount,
            paymentsMadeThisCycle,
            utilizationRatio,
            isOverdue);
    }
}
