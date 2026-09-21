using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;

namespace TrackTraceMoney.App.Converters;

/// <summary>
/// Renders the icon picker's "no icon" sentinel (empty string, <see cref="Models.CategoryIconPalette.NoIconValue"/>)
/// as a localized placeholder label, and every other value (a raw emoji glyph -- self-describing,
/// like every other emoji-as-icon usage already in this app, e.g. <c>TransactionLabelFormatter</c>) as
/// itself, unconverted.
/// </summary>
public sealed class CategoryIconToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string icon && !string.IsNullOrEmpty(icon) ? icon : AppResources.AddCategory_NoIconOption;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
