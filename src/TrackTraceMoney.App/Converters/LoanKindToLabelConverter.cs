using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.Converters;

public sealed class LoanKindToLabelConverter : EnumToLabelConverter<LoanKind>
{
    protected override string GetLabel(LoanKind value) => value switch
    {
        LoanKind.PersonalLoan => AppResources.LoanKind_PersonalLoan,
        LoanKind.AutoLoan => AppResources.LoanKind_AutoLoan,
        LoanKind.Mortgage => AppResources.LoanKind_Mortgage,
        LoanKind.BankCredit => AppResources.LoanKind_BankCredit,
        LoanKind.InstallmentPurchase => AppResources.LoanKind_InstallmentPurchase,
        LoanKind.OtherLoan => AppResources.LoanKind_OtherLoan,
        _ => string.Empty
    };
}
