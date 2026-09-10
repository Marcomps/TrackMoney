using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.Converters;

public sealed class TermDepositRateTypeToLabelConverter : EnumToLabelConverter<TermDepositRateType>
{
    protected override string GetLabel(TermDepositRateType value) => value switch
    {
        TermDepositRateType.Nominal => AppResources.TermDepositRateType_Nominal,
        TermDepositRateType.Effective => AppResources.TermDepositRateType_Effective,
        _ => string.Empty
    };
}
