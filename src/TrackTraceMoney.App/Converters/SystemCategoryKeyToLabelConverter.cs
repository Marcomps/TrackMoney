using System.Globalization;
using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.App.Converters;

public sealed class SystemCategoryKeyToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is SystemCategoryKey key ? GetLabel(key) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static string GetLabel(SystemCategoryKey key) => key switch
    {
        SystemCategoryKey.Food => AppResources.Category_Food,
        SystemCategoryKey.Housing => AppResources.Category_Housing,
        SystemCategoryKey.Transportation => AppResources.Category_Transportation,
        SystemCategoryKey.Health => AppResources.Category_Health,
        SystemCategoryKey.Education => AppResources.Category_Education,
        SystemCategoryKey.Entertainment => AppResources.Category_Entertainment,
        SystemCategoryKey.Shopping => AppResources.Category_Shopping,
        SystemCategoryKey.Utilities => AppResources.Category_Utilities,
        SystemCategoryKey.Subscriptions => AppResources.Category_Subscriptions,
        SystemCategoryKey.Debts => AppResources.Category_Debts,
        SystemCategoryKey.Insurance => AppResources.Category_Insurance,
        SystemCategoryKey.Investments => AppResources.Category_Investments,
        SystemCategoryKey.Savings => AppResources.Category_Savings,
        SystemCategoryKey.Other => AppResources.Category_Other,
        _ => string.Empty
    };

    /// <summary>
    /// Resolves the display name for a category: the localized system label for system-defined
    /// categories, or the user's own typed name for user-defined ones (there is nothing to translate).
    /// </summary>
    public static string GetDisplayName(Category category) =>
        category.IsSystemDefined ? GetLabel(category.SystemKey) : category.Name;
}
