using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Converters;

public sealed class AccountKindToLabelConverter : EnumToLabelConverter<AccountKind>
{
    protected override string GetLabel(AccountKind value) => value switch
    {
        AccountKind.Cash => AppResources.AccountKind_Cash,
        AccountKind.Bank => AppResources.AccountKind_Bank,
        AccountKind.Savings => AppResources.AccountKind_Savings,
        _ => string.Empty
    };
}
