using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Categories;

/// <summary>
/// A spend/income category (README §12). System-defined categories carry a <see cref="SystemKey"/>
/// so the App layer can resolve a localized display name; user-defined ones just use <see cref="Name"/>
/// as typed, since it's the user's own text and there's nothing to translate.
/// </summary>
public sealed class Category : Entity
{
    public string Name { get; private set; } = null!;

    public SystemCategoryKey SystemKey { get; private set; } = SystemCategoryKey.None;

    public string? Icon { get; private set; }

    public bool IsSystemDefined => SystemKey != SystemCategoryKey.None;

    private Category()
    {
    }

    private Category(string name, SystemCategoryKey systemKey, string? icon)
    {
        Rename(name);
        SystemKey = systemKey;
        Icon = icon;
    }

    public static Category CreateUserDefined(string name, string? icon = null) =>
        new(name, SystemCategoryKey.None, icon);

    public static Category CreateSystemDefined(SystemCategoryKey key, string defaultName, string? icon = null)
    {
        if (key == SystemCategoryKey.None)
            throw new ArgumentException("System categories require a specific key.", nameof(key));

        return new Category(defaultName, key, icon);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void SetIcon(string? icon) => Icon = icon;
}
