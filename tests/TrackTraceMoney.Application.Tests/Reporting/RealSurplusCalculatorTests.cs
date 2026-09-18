using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Tests.Reporting;

/// <summary>
/// README §35's real surplus formula: available balance minus upcoming expenses minus debt payments,
/// per currency, never blended across currencies (CLAUDE.md hard constraint).
/// </summary>
public sealed class RealSurplusCalculatorTests
{
    [Fact]
    public void Calculate_MatchesReadmeWorkedExample()
    {
        // README §35's own worked example: $1,000 available, $200 upcoming expenses, $250 debt
        // payments -> $550 real surplus (acceptance criterion 1).
        var summary = new RealSurplusCalculator().Calculate(
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 1000m },
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 200m },
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 250m });

        var row = summary.ByCurrency[CurrencyCode.USD];
        Assert.Equal(1000m, row.AvailableBalance);
        Assert.Equal(200m, row.UpcomingExpenses);
        Assert.Equal(250m, row.DebtPayments);
        Assert.Equal(550m, row.Amount);
    }

    [Fact]
    public void Calculate_CurrencyPresentOnlyInOneInput_StillYieldsARowWithOthersAtZero()
    {
        // A currency with active debt but zero available-balance accounts must still produce a row
        // (acceptance criterion 5) — proven here for each of the three inputs independently via
        // GetValueOrDefault, mirroring NetWorthCalculatorTests's equivalent one-sided-currency test.
        var summary = new RealSurplusCalculator().Calculate(
            new Dictionary<CurrencyCode, decimal>(),
            new Dictionary<CurrencyCode, decimal>(),
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 300m });

        var row = summary.ByCurrency[CurrencyCode.USD];
        Assert.Equal(0m, row.AvailableBalance);
        Assert.Equal(0m, row.UpcomingExpenses);
        Assert.Equal(300m, row.DebtPayments);
        Assert.Equal(-300m, row.Amount);
    }

    [Fact]
    public void Calculate_MultiCurrencyIsolation_NeverCombinesUsdAndMxn()
    {
        var summary = new RealSurplusCalculator().Calculate(
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 500m, [CurrencyCode.MXN] = 8000m },
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100m },
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.MXN] = 1000m });

        var usd = summary.ByCurrency[CurrencyCode.USD];
        Assert.Equal(500m, usd.AvailableBalance);
        Assert.Equal(100m, usd.UpcomingExpenses);
        Assert.Equal(0m, usd.DebtPayments);
        Assert.Equal(400m, usd.Amount);

        var mxn = summary.ByCurrency[CurrencyCode.MXN];
        Assert.Equal(8000m, mxn.AvailableBalance);
        Assert.Equal(0m, mxn.UpcomingExpenses);
        Assert.Equal(1000m, mxn.DebtPayments);
        Assert.Equal(7000m, mxn.Amount);
    }

    [Fact]
    public void Calculate_EmptyInputs_ReturnsEmptyByCurrency()
    {
        var summary = new RealSurplusCalculator().Calculate(
            new Dictionary<CurrencyCode, decimal>(),
            new Dictionary<CurrencyCode, decimal>(),
            new Dictionary<CurrencyCode, decimal>());

        Assert.Empty(summary.ByCurrency);
    }

    [Fact]
    public void Calculate_ObligationsExceedBalance_ResultIsNegative_NotFlooredAtZero()
    {
        // No floor anywhere in this calculation — insolvency for a currency is a legitimate state to
        // surface (mirrors NetWorthCalculatorTests.Calculate_NetWorthCanBeNegativeForACurrency).
        var summary = new RealSurplusCalculator().Calculate(
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100m },
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 50m },
            new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 200m });

        Assert.Equal(-150m, summary.ByCurrency[CurrencyCode.USD].Amount);
    }
}
