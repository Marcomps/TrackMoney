using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;
using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.Application.Tests.Reporting;

public sealed class CashFlowProjectionCalculatorTests
{
    private static readonly Guid UsdAccount = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, CurrencyCode> Currencies =
        new Dictionary<Guid, CurrencyCode> { [UsdAccount] = CurrencyCode.USD };

    private static RecurringIncome Salary(DateOnly start) =>
        new("Salario", 654.52m, Guid.NewGuid(), UsdAccount, RecurringIncomeFrequency.SemiMonthly, start, endDate: null);

    [Fact]
    public void ExpectedIncomeThrough_SemiMonthly_CountsEachPaydayInTheWindow()
    {
        var salary = Salary(new DateOnly(2026, 9, 30));

        var throughSeptember = CashFlowProjectionCalculator.ExpectedIncomeThrough([salary], new DateOnly(2026, 9, 30), Currencies);
        var throughOctober = CashFlowProjectionCalculator.ExpectedIncomeThrough([salary], new DateOnly(2026, 10, 31), Currencies);

        Assert.Equal(654.52m, throughSeptember[CurrencyCode.USD]);
        Assert.Equal(1963.56m, throughOctober[CurrencyCode.USD]); // 30/9 + 15/10 + 31/10
    }

    [Fact]
    public void ExpectedIncomeThrough_ConfirmedPaydayIsNoLongerExpected()
    {
        var salary = Salary(new DateOnly(2026, 9, 30));
        salary.MarkConfirmed(new DateOnly(2026, 9, 30));

        var result = CashFlowProjectionCalculator.ExpectedIncomeThrough([salary], new DateOnly(2026, 9, 30), Currencies);

        Assert.False(result.ContainsKey(CurrencyCode.USD));
    }

    [Fact]
    public void ExpectedIncomeThrough_UnresolvableAccount_IsSkipped_NotMisattributed()
    {
        var orphan = new RecurringIncome("X", 100m, Guid.NewGuid(), Guid.NewGuid(), RecurringIncomeFrequency.Monthly, new DateOnly(2026, 9, 30), null);

        var result = CashFlowProjectionCalculator.ExpectedIncomeThrough([orphan], new DateOnly(2026, 9, 30), Currencies);

        Assert.Empty(result);
    }

    [Fact]
    public void Project_AddsExpectedIncomeOnTopOfRealSurplus_WithoutChangingIt()
    {
        // Agricola 203.81; subscriptions 6.99 + 25 + 20 + 10.99 = 62.98 due this month; salary 654.52 on the 30th.
        var surplus = new RealSurplusSummary
        {
            ByCurrency = new Dictionary<CurrencyCode, RealSurplus>
            {
                [CurrencyCode.USD] = RealSurplus.Calculate(CurrencyCode.USD, 203.81m, 62.98m, 0m),
            },
        };

        var projection = Assert.Single(CashFlowProjectionCalculator.Project(
            surplus, new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 654.52m }));

        Assert.Equal(140.83m, surplus.ByCurrency[CurrencyCode.USD].Amount); // real surplus stays income-free
        Assert.Equal(654.52m, projection.ExpectedIncome);
        Assert.Equal(795.35m, projection.Projected); // 203.81 + 654.52 - 62.98
    }

    [Fact]
    public void Project_CurrencyWithOnlyExpectedIncome_StillGetsARow()
    {
        var projection = Assert.Single(CashFlowProjectionCalculator.Project(
            new RealSurplusSummary(), new Dictionary<CurrencyCode, decimal> { [CurrencyCode.USD] = 100m }));

        Assert.Equal(CurrencyCode.USD, projection.Currency);
        Assert.Equal(100m, projection.Projected);
    }
}
