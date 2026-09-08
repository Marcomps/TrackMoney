using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.App.Converters;

public sealed class RecurringExpenseFrequencyToLabelConverter : EnumToLabelConverter<RecurringExpenseFrequency>
{
    protected override string GetLabel(RecurringExpenseFrequency value) => value switch
    {
        RecurringExpenseFrequency.Weekly => AppResources.RecurringExpenseFrequency_Weekly,
        RecurringExpenseFrequency.Monthly => AppResources.RecurringExpenseFrequency_Monthly,
        RecurringExpenseFrequency.Yearly => AppResources.RecurringExpenseFrequency_Yearly,
        _ => string.Empty
    };
}
