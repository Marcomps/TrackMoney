using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.App.Converters;

public sealed class TransactionPresetBaseTypeToLabelConverter : EnumToLabelConverter<TransactionPresetBaseType>
{
    protected override string GetLabel(TransactionPresetBaseType value) => value switch
    {
        TransactionPresetBaseType.Expense => AppResources.TransactionPresetBaseType_Expense,
        TransactionPresetBaseType.Income => AppResources.TransactionPresetBaseType_Income,
        TransactionPresetBaseType.Transfer => AppResources.TransactionPresetBaseType_Transfer,
        _ => string.Empty
    };
}
