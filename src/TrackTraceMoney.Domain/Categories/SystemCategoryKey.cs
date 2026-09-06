namespace TrackTraceMoney.Domain.Categories;

/// <summary>
/// Initial categories from README §12. <see cref="None"/> marks a user-defined category, whose
/// display name is whatever the user typed rather than a translated resource.
/// </summary>
public enum SystemCategoryKey
{
    None,
    Food,
    Housing,
    Transportation,
    Health,
    Education,
    Entertainment,
    Shopping,
    Utilities,
    Subscriptions,
    Debts,
    Insurance,
    Investments,
    Savings,
    Other
}
