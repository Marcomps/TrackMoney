using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.App.Models;

public sealed record CategoryListItem(Guid Id, string Name, bool IsSystemDefined)
{
    public static CategoryListItem FromDomain(Category category, string resolvedName) =>
        new(category.Id, resolvedName, category.IsSystemDefined);
}
