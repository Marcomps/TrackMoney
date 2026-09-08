using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.App.Converters;

public sealed class RecurringExpenseFrequencyToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RecurringExpenseFrequency.Weekly => AppResources.RecurringExpenseFrequency_Weekly,
        RecurringExpenseFrequency.Monthly => AppResources.RecurringExpenseFrequency_Monthly,
        RecurringExpenseFrequency.Yearly => AppResources.RecurringExpenseFrequency_Yearly,
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
