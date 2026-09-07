using System.Globalization;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Converters;

public sealed class TransactionTypeToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TransactionType.Expense => AppResources.TransactionType_Expense,
        TransactionType.Income => AppResources.TransactionType_Income,
        TransactionType.Transfer => AppResources.TransactionType_Transfer,
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
