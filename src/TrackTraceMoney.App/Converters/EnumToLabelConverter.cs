using System.Globalization;

namespace TrackTraceMoney.App.Converters;

/// <summary>
/// Shared skeleton for this app's several one-way enum-to-localized-label <see cref="IValueConverter"/>s
/// (<see cref="AccountKindToLabelConverter"/>, <see cref="TransactionTypeToLabelConverter"/>,
/// <see cref="RecurringExpenseFrequencyToLabelConverter"/>, <see cref="SystemCategoryKeyToLabelConverter"/>).
/// Handles the common boilerplate — the value/type check and the "never convert back" behavior — so
/// each derived converter only needs to supply its own enum-to-<c>AppResources</c> string switch via
/// <see cref="GetLabel"/>.
/// </summary>
public abstract class EnumToLabelConverter<TEnum> : IValueConverter
    where TEnum : struct, Enum
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TEnum enumValue ? GetLabel(enumValue) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    protected abstract string GetLabel(TEnum value);
}
