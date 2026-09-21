using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.Domain.Tests.Categories;

/// <summary>
/// Covers <see cref="Category"/>'s mutators (Category lifecycle slice §A.3/§A.5) -- construction-time
/// coverage for <see cref="Category.SetIcon"/>/<see cref="Category.Rename"/> already existed implicitly
/// via the constructors, but neither had a dedicated mutator test since neither had a real UI caller
/// until this slice.
/// </summary>
public sealed class CategoryTests
{
    [Fact]
    public void CreateUserDefined_DefaultsToActive()
    {
        var category = Category.CreateUserDefined("Mascotas");

        Assert.True(category.IsActive);
    }

    [Fact]
    public void CreateSystemDefined_DefaultsToActive()
    {
        var category = Category.CreateSystemDefined(SystemCategoryKey.Food, "Food");

        Assert.True(category.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var category = Category.CreateUserDefined("Mascotas");

        category.Deactivate();

        Assert.False(category.IsActive);
    }

    [Fact]
    public void Reactivate_SetsIsActiveTrue()
    {
        var category = Category.CreateUserDefined("Mascotas");
        category.Deactivate();

        category.Reactivate();

        Assert.True(category.IsActive);
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var category = Category.CreateUserDefined("Mascotas");

        category.Rename("Veterinario");

        Assert.Equal("Veterinario", category.Name);
    }

    [Fact]
    public void Rename_TrimsWhitespace()
    {
        var category = Category.CreateUserDefined("Mascotas");

        category.Rename("  Veterinario  ");

        Assert.Equal("Veterinario", category.Name);
    }

    [Fact]
    public void Rename_EmptyName_Throws()
    {
        var category = Category.CreateUserDefined("Mascotas");

        Assert.Throws<ArgumentException>(() => category.Rename(" "));
    }

    [Fact]
    public void SetIcon_UpdatesIcon()
    {
        var category = Category.CreateUserDefined("Mascotas");

        category.SetIcon("🐕");

        Assert.Equal("🐕", category.Icon);
    }

    [Fact]
    public void SetIcon_Null_ClearsIcon()
    {
        var category = Category.CreateUserDefined("Mascotas", icon: "🐕");

        category.SetIcon(null);

        Assert.Null(category.Icon);
    }
}
