using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Tests.Common;

public sealed class RecurrenceDatesTests
{
    [Theory]
    [InlineData(2026, 9, 1, 2026, 9, 15)]
    [InlineData(2026, 9, 15, 2026, 9, 15)]
    [InlineData(2026, 9, 16, 2026, 9, 30)]
    [InlineData(2026, 9, 25, 2026, 9, 30)]
    [InlineData(2027, 2, 20, 2027, 2, 28)]
    [InlineData(2028, 2, 20, 2028, 2, 29)]
    public void HalfMonthEnd_Is15thOrLastDayOfMonth(int y, int m, int d, int ey, int em, int ed)
    {
        Assert.Equal(new DateOnly(ey, em, ed), RecurrenceDates.HalfMonthEnd(new DateOnly(y, m, d)));
    }
}
