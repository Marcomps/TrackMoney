namespace TrackTraceMoney.Application.CreditAccounts;

/// <summary>
/// One debt's data as fed into <see cref="ISnowballPlanner"/> — already resolved to a single minimum
/// payment figure (README §20), sourced by the caller per debt subtype (Loan.RequiredPayment;
/// CreditCard's latest CreditCardStatement.MinimumPayment, or excluded entirely if no statement
/// exists). Deliberately currency-agnostic: the caller partitions debts into same-currency groups
/// before building this list, since balances in different currencies must never be ranked together.
/// </summary>
public sealed record SnowballDebtInput(
    Guid CreditAccountId,
    string Name,
    decimal AmountOwed,
    decimal MinimumPayment,
    DateTimeOffset CreatedAtUtc);
