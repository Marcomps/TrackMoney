using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

public sealed record TermDepositListItem(
    Guid Id,
    string Name,
    string Institution,
    CurrencyCode Currency,
    decimal Balance,
    DateOnly MaturityDate,
    decimal Rate,
    TermDepositRateType RateType)
{
    public static TermDepositListItem FromDomain(TermDeposit termDeposit) => new(
        termDeposit.Id, termDeposit.Name, termDeposit.Institution, termDeposit.Currency,
        termDeposit.Balance, termDeposit.MaturityDate, termDeposit.Rate, termDeposit.RateType);
}
