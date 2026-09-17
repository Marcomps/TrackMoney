using TrackTraceMoney.Domain.Institutions;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in the financial institutions list (README §14/§19/§22/§23 bank/issuer source data).</summary>
public sealed record FinancialInstitutionListItem(Guid Id, string Name)
{
    public static FinancialInstitutionListItem FromDomain(FinancialInstitution institution) =>
        new(institution.Id, institution.Name);
}
