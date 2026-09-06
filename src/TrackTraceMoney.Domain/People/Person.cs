using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.People;

/// <summary>
/// A person expenses can be paid by or paid for (README §7) — lets the app separate
/// "quién pagó" from "para quién fue el gasto".
/// </summary>
public sealed class Person : Entity
{
    public string Name { get; private set; } = null!;

    public PersonRelationshipType RelationshipType { get; private set; }

    public string? CustomRelationshipLabel { get; private set; }

    public string? Notes { get; private set; }

    private Person()
    {
    }

    public Person(string name, PersonRelationshipType relationshipType, string? customRelationshipLabel = null, string? notes = null)
    {
        Rename(name);
        SetRelationship(relationshipType, customRelationshipLabel);
        Notes = notes;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Person name cannot be empty.", nameof(name));

        Name = name.Trim();
    }

    public void SetRelationship(PersonRelationshipType relationshipType, string? customRelationshipLabel)
    {
        if (relationshipType == PersonRelationshipType.Custom && string.IsNullOrWhiteSpace(customRelationshipLabel))
            throw new ArgumentException("A custom relationship requires a label.", nameof(customRelationshipLabel));

        RelationshipType = relationshipType;
        CustomRelationshipLabel = relationshipType == PersonRelationshipType.Custom
            ? customRelationshipLabel!.Trim()
            : null;
    }

    public void UpdateNotes(string? notes) => Notes = notes;
}
