using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record CreditCardListItem(
    Guid Id,
    string Name,
    string Issuer,
    string? LastFourDigits,
    CurrencyCode Currency,
    decimal CreditLimit,
    decimal AmountOwed,
    decimal AvailableCredit)
{
    public bool HasLastFourDigits => !string.IsNullOrEmpty(LastFourDigits);

    public string MaskedLastFourDigits => HasLastFourDigits ? $"•••• {LastFourDigits}" : string.Empty;

    public static CreditCardListItem FromDomain(CreditCard card) => new(
        card.Id,
        card.Name,
        card.Issuer,
        card.LastFourDigits,
        card.Currency,
        card.CreditLimit,
        card.AmountOwed,
        card.AvailableCredit);
}
