namespace TrackTraceMoney.Domain.People;

/// <summary>
/// Predefined relationships from README §7. <see cref="Custom"/> carries its label in
/// <see cref="Person.CustomRelationshipLabel"/> instead of a translated resource, since it's the
/// user's own free text rather than app UI copy.
/// </summary>
public enum PersonRelationshipType
{
    Me,
    Partner,
    Child,
    Parent,
    Family,
    Other,
    Custom
}
