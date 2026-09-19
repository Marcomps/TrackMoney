using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record LoanListItem(
    Guid Id,
    string Name,
    string InstitutionName,
    LoanKind Kind,
    CurrencyCode Currency,
    decimal OriginalAmount,
    decimal AmountOwed,
    decimal InterestRate,
    LoanRateType RateType,
    decimal MonthlyInstallment,
    DateOnly NextPaymentDate,
    int RemainingPayments,
    decimal RequiredPayment,
    bool IsActive)
{
    /// <summary>Same rationale as <c>AccountListItem.IsInactive</c> (edit/delete slice spec §1.3/§3.3).</summary>
    public bool IsInactive => !IsActive;

    public static LoanListItem FromDomain(Loan loan, string institutionName) => new(
        loan.Id, loan.Name, institutionName, loan.Kind, loan.Currency,
        loan.OriginalAmount, loan.AmountOwed, loan.InterestRate, loan.RateType,
        loan.MonthlyInstallment, loan.NextPaymentDate, loan.RemainingPayments, loan.RequiredPayment,
        loan.IsActive);
}
