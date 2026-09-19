using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.App.Converters;

public sealed class RecurringIncomeFrequencyToLabelConverter : EnumToLabelConverter<RecurringIncomeFrequency>
{
    protected override string GetLabel(RecurringIncomeFrequency value) => value switch
    {
        RecurringIncomeFrequency.Weekly => AppResources.RecurringIncomeFrequency_Weekly,
        RecurringIncomeFrequency.Biweekly => AppResources.RecurringIncomeFrequency_Biweekly,
        RecurringIncomeFrequency.Monthly => AppResources.RecurringIncomeFrequency_Monthly,
        RecurringIncomeFrequency.Yearly => AppResources.RecurringIncomeFrequency_Yearly,
        _ => string.Empty
    };
}
