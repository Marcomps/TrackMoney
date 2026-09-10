using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.App.Converters;

public sealed class TermDepositInterestFrequencyToLabelConverter : EnumToLabelConverter<TermDepositInterestFrequency>
{
    protected override string GetLabel(TermDepositInterestFrequency value) => value switch
    {
        TermDepositInterestFrequency.Monthly => AppResources.TermDepositInterestFrequency_Monthly,
        TermDepositInterestFrequency.Quarterly => AppResources.TermDepositInterestFrequency_Quarterly,
        TermDepositInterestFrequency.Semiannual => AppResources.TermDepositInterestFrequency_Semiannual,
        TermDepositInterestFrequency.Annual => AppResources.TermDepositInterestFrequency_Annual,
        TermDepositInterestFrequency.AtMaturity => AppResources.TermDepositInterestFrequency_AtMaturity,
        _ => string.Empty
    };
}
