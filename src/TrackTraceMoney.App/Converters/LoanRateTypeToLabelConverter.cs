using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.App.Converters;

public sealed class LoanRateTypeToLabelConverter : EnumToLabelConverter<LoanRateType>
{
    protected override string GetLabel(LoanRateType value) => value switch
    {
        LoanRateType.Fixed => AppResources.LoanRateType_Fixed,
        LoanRateType.Variable => AppResources.LoanRateType_Variable,
        _ => string.Empty
    };
}
