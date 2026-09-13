using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.NetWorth;

namespace TrackTraceMoney.Domain.Tests.NetWorth;

public sealed class NetWorthSnapshotTests
{
    [Fact]
    public void Constructor_SetsFieldsAndComputesNetWorth()
    {
        var snapshot = new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 1, 1), 1000m, 400m);

        Assert.Equal(CurrencyCode.USD, snapshot.Currency);
        Assert.Equal(new DateOnly(2026, 1, 1), snapshot.AsOfDate);
        Assert.Equal(1000m, snapshot.TotalAssets);
        Assert.Equal(400m, snapshot.TotalLiabilities);
        Assert.Equal(600m, snapshot.NetWorth);
    }

    [Fact]
    public void Constructor_WithNegativeTotalLiabilities_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 1, 1), 1000m, -1m));
    }

    [Fact]
    public void Constructor_AllowsNegativeTotalAssets_AndNetWorthReflectsIt()
    {
        var snapshot = new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 1, 1), -200m, 100m);

        Assert.Equal(-200m, snapshot.TotalAssets);
        Assert.Equal(-300m, snapshot.NetWorth);
    }

    [Fact]
    public void UpdateTotals_UpdatesBothFields()
    {
        var snapshot = new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 1, 1), 1000m, 400m);

        snapshot.UpdateTotals(1200m, 300m);

        Assert.Equal(1200m, snapshot.TotalAssets);
        Assert.Equal(300m, snapshot.TotalLiabilities);
        Assert.Equal(900m, snapshot.NetWorth);
    }

    [Fact]
    public void UpdateTotals_WithNegativeTotalLiabilities_Throws()
    {
        var snapshot = new NetWorthSnapshot(CurrencyCode.USD, new DateOnly(2026, 1, 1), 1000m, 400m);

        Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.UpdateTotals(1000m, -1m));
    }
}
