using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Accounts;

/// <summary>
/// A point-in-time record of an <see cref="InvestmentFund"/>'s value (README §23: "A history of the
/// investment's value can be stored to generate charts"). User-initiated, not auto-generated — mirrors
/// CreditCardStatement's "periodic snapshot tied to a parent account" shape. Unlike CreditCardStatement,
/// both AsOfDate and Value are user-entered (neither is computed from account configuration), so both
/// are revisable together via UpdateValuation rather than splitting immutable-date/revisable-amount.
/// </summary>
public sealed class InvestmentValuation : Entity
{
    public Guid InvestmentFundId { get; private set; }
    public DateOnly AsOfDate { get; private set; }
    public decimal Value { get; private set; }

    private InvestmentValuation() { }

    public InvestmentValuation(Guid investmentFundId, DateOnly asOfDate, decimal value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be negative.");

        InvestmentFundId = investmentFundId;
        AsOfDate = asOfDate;
        Value = value;
    }

    public void UpdateValuation(decimal value, DateOnly asOfDate)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Value cannot be negative.");

        Value = value;
        AsOfDate = asOfDate;
    }
}
