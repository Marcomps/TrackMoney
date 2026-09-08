using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.App.Converters;

public sealed class SystemCategoryKeyToLabelConverter : EnumToLabelConverter<SystemCategoryKey>
{
    protected override string GetLabel(SystemCategoryKey value) => ResolveLabel(value);

    /// <summary>
    /// Resolves the display name for a category: the localized system label for system-defined
    /// categories, or the user's own typed name for user-defined ones (there is nothing to translate).
    /// </summary>
    public static string GetDisplayName(Category category) =>
        category.IsSystemDefined ? ResolveLabel(category.SystemKey) : category.Name;

    private static string ResolveLabel(SystemCategoryKey key) => key switch
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
}
