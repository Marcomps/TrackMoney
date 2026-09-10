using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record LoanListItem(
    Guid Id,
    string Name,
    string Institution,
    LoanKind Kind,
    CurrencyCode Currency,
    decimal OriginalAmount,
    decimal AmountOwed,
    decimal InterestRate,
    LoanRateType RateType,
    decimal MonthlyInstallment,
    DateOnly NextPaymentDate,
    int RemainingPayments,
    decimal RequiredPayment)
{
    public static LoanListItem FromDomain(Loan loan) => new(
        loan.Id, loan.Name, loan.Institution, loan.Kind, loan.Currency,
        loan.OriginalAmount, loan.AmountOwed, loan.InterestRate, loan.RateType,
        loan.MonthlyInstallment, loan.NextPaymentDate, loan.RemainingPayments, loan.RequiredPayment);
}
