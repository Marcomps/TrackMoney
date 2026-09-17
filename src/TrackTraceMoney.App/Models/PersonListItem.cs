using TrackTraceMoney.Domain.People;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in the people list (README §7 Payer/Beneficiary source data).</summary>
public sealed record PersonListItem(Guid Id, string Name, string RelationshipLabel)
{
    public static PersonListItem FromDomain(Person person, string relationshipLabel) =>
        new(person.Id, person.Name, relationshipLabel);
}
