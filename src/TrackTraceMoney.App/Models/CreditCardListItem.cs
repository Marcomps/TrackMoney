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
    string HealthEmoji,
    bool IsActive)
{
    public bool HasLastFourDigits => !string.IsNullOrEmpty(LastFourDigits);

    public string MaskedLastFourDigits => HasLastFourDigits ? $"•••• {LastFourDigits}" : string.Empty;

    /// <summary>Same rationale as <c>AccountListItem.IsInactive</c> (edit/delete slice spec §2.2).</summary>
    public bool IsInactive => !IsActive;

    public bool HasCreditBalance => AmountOwed < 0m;

    public bool ShowsAmountOwed => !HasCreditBalance;

    /// <summary>"Saldo a favor: 10.78" when the card was overpaid; empty otherwise.</summary>
    public string CreditBalanceText => HasCreditBalance
        ? string.Format(System.Globalization.CultureInfo.CurrentCulture, TrackTraceMoney.App.Resources.Strings.AppResources.CreditCards_CreditBalanceFormat, -AmountOwed)
        : string.Empty;

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
        CreditCardHealthIndicator.GetEmoji(healthStatus),
        card.IsActive);
}
