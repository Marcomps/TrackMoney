using TrackTraceMoney.App.Resources.Strings;
using TrackTraceMoney.Domain.People;

namespace TrackTraceMoney.App.Converters;

public sealed class PersonRelationshipTypeToLabelConverter : EnumToLabelConverter<PersonRelationshipType>
{
    protected override string GetLabel(PersonRelationshipType value) => ResolveLabel(value);

    /// <summary>
    /// Resolves the display label for a person's relationship: the user's own typed label for
    /// <see cref="PersonRelationshipType.Custom"/> (there is nothing to translate — mirrors
    /// <see cref="SystemCategoryKeyToLabelConverter.GetDisplayName"/>'s user-defined-category case), or
    /// the localized label for every predefined relationship.
    /// </summary>
    public static string GetDisplayLabel(Person person) =>
        person.RelationshipType == PersonRelationshipType.Custom
            ? person.CustomRelationshipLabel ?? string.Empty
            : ResolveLabel(person.RelationshipType);

    private static string ResolveLabel(PersonRelationshipType value) => value switch
    {
        PersonRelationshipType.Me => AppResources.PersonRelationshipType_Me,
        PersonRelationshipType.Partner => AppResources.PersonRelationshipType_Partner,
        PersonRelationshipType.Child => AppResources.PersonRelationshipType_Child,
        PersonRelationshipType.Parent => AppResources.PersonRelationshipType_Parent,
        PersonRelationshipType.Family => AppResources.PersonRelationshipType_Family,
        PersonRelationshipType.Other => AppResources.PersonRelationshipType_Other,
        PersonRelationshipType.Custom => AppResources.PersonRelationshipType_Custom,
        _ => string.Empty
    };
}
