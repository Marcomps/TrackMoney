using TrackTraceMoney.Application.CreditAccounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record CreditCardListItem(
    Guid Id,
    string Name,
    string InstitutionName,
    string? LastFourDigits,
    CurrencyCode Currency,
    decimal CreditLimit,
    decimal AmountOwed,
    decimal AvailableCredit,
    CreditCardHealthStatus HealthStatus,
    string HealthEmoji)
{
    public bool HasLastFourDigits => !string.IsNullOrEmpty(LastFourDigits);

    public string MaskedLastFourDigits => HasLastFourDigits ? $"•••• {LastFourDigits}" : string.Empty;

    public static CreditCardListItem FromDomain(CreditCard card, CreditCardHealthStatus healthStatus, string institutionName) => new(
        card.Id,
        card.Name,
        institutionName,
        card.LastFourDigits,
        card.Currency,
        card.CreditLimit,
        card.AmountOwed,
        card.AvailableCredit,
        healthStatus,
        CreditCardHealthIndicator.GetEmoji(healthStatus));
}
