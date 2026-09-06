using System.Globalization;
using TrackTraceMoney.App.Models;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Converters;

public sealed class AccountKindToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AccountKind.Cash => AppResources.AccountKind_Cash,
        AccountKind.Bank => AppResources.AccountKind_Bank,
        AccountKind.Savings => AppResources.AccountKind_Savings,
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
