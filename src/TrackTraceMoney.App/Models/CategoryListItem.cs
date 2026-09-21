using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.App.Models;

public sealed record CategoryListItem(Guid Id, string Name, string? Icon, bool IsSystemDefined, bool IsActive)
{
    public bool IsInactive => !IsActive;

    /// <summary>Drives the icon <c>Label</c>'s <c>IsVisible</c> in <c>CategoriesListPage</c> -- a plain
    /// computed property rather than a string-to-bool XAML converter, mirroring <see cref="IsInactive"/>'s
    /// own precedent.</summary>
    public bool HasIcon => !string.IsNullOrEmpty(Icon);

    /// <summary>
    /// Drives whether <c>CategoriesListPage</c>'s Deactivate/Delete <c>SwipeItem</c>s are shown for this
    /// row (Category lifecycle slice §A.2/A-2) -- <see cref="Domain.Categories.Category.IsSystemDefined"/>
    /// categories can never be deactivated/deleted, so those actions are hidden here rather than left to
    /// fail against <c>CategoryLifecycleService</c>'s defensive throw. <c>SwipeItem</c> (a <c>MenuItem</c>,
    /// not a <c>VisualElement</c>) has no <c>Triggers</c> collection to combine two bindings with, so this
    /// is a plain computed property instead, mirroring <see cref="IsInactive"/>/<see cref="HasIcon"/>.
    /// </summary>
    public bool CanDeactivateOrDelete => IsActive && !IsSystemDefined;

    public static CategoryListItem FromDomain(Category category, string resolvedName) =>
        new(category.Id, resolvedName, category.Icon, category.IsSystemDefined, category.IsActive);
}
