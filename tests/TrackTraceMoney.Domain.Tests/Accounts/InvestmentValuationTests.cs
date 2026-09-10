using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Domain.Tests.Accounts;

public sealed class InvestmentValuationTests
{
    [Fact]
    public void Constructor_WithNegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InvestmentValuation(Guid.NewGuid(), new DateOnly(2026, 1, 1), -1m));
    }

    [Fact]
    public void UpdateValuation_WithNegativeValue_Throws()
    {
        var valuation = new InvestmentValuation(Guid.NewGuid(), new DateOnly(2026, 1, 1), 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => valuation.UpdateValuation(-1m, new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void UpdateValuation_ChangesBothValueAndAsOfDate()
    {
        var valuation = new InvestmentValuation(Guid.NewGuid(), new DateOnly(2026, 1, 1), 100m);

        valuation.UpdateValuation(150m, new DateOnly(2026, 2, 1));

        Assert.Equal(150m, valuation.Value);
        Assert.Equal(new DateOnly(2026, 2, 1), valuation.AsOfDate);
    }
}
