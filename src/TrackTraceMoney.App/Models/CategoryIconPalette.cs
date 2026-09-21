namespace TrackTraceMoney.App.Models;

/// <summary>
/// Fixed emoji palette backing the category icon picker (Category lifecycle slice §A.2) -- the
/// simplest possible implementation per the slice spec: no image-asset/icon-library system exists
/// anywhere in this app, so introducing one now would be a disproportionate addition. Shared between
/// <c>AddCategoryViewModel</c> and <c>EditCategoryViewModel</c> so both pickers offer the identical set.
/// </summary>
public static class CategoryIconPalette
{
    /// <summary>
    /// Sentinel representing "no icon" in the picker's <see cref="Icons"/> list -- never itself a real
    /// icon value. <see cref="Domain.Categories.Category.Icon"/> stores <see langword="null"/> for "no
    /// icon", not this sentinel; callers translate at the ViewModel boundary (mirrors
    /// <c>AddAccountViewModel.AsNullableId</c>'s same "None" sentinel convention for pickers).
    /// </summary>
    public const string NoIconValue = "";

    public static IReadOnlyList<string> Icons { get; } =
    [
        NoIconValue,
        "🍔", // 🍔 Food
        "🏠", // 🏠 Housing
        "🚗", // 🚗 Transportation
        "🏥", // 🏥 Health
        "🎓", // 🎓 Education
        "🎮", // 🎮 Entertainment
        "🛍️", // 🛍️ Shopping
        "💡", // 💡 Utilities
        "🔁", // 🔁 Subscriptions
        "💳", // 💳 Debts
        "🛡️", // 🛡️ Insurance
        "📈", // 📈 Investments
        "🐷", // 🐖 Savings
        "🐾", // 🐾 Pets
        "✈️", // ✈️ Travel
        "🎁", // 🎁 Gifts
        "☕", // ☕ Coffee
        "📦", // 📦 Other
    ];
}
