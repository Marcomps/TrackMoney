using TrackTraceMoney.Application.RecurringIncomes;
using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.Application.Tests.RecurringIncomes;

public sealed class RecurringIncomeEquivalentCalculatorTests
{
    [Theory]
    [InlineData(RecurringIncomeFrequency.Weekly, 100, 433.33)] // 100 * 52 / 12
    [InlineData(RecurringIncomeFrequency.Biweekly, 160, 346.67)] // 160 * 26 / 12
    [InlineData(RecurringIncomeFrequency.Monthly, 2000, 2000)]
    [InlineData(RecurringIncomeFrequency.Yearly, 24000, 2000)]
    public void ToMonthlyEquivalent_ProducesExpectedFigure(RecurringIncomeFrequency frequency, decimal amount, decimal expected)
    {
        var result = RecurringIncomeEquivalentCalculator.ToMonthlyEquivalent(amount, frequency);

        Assert.Equal(expected, Math.Round(result, 2));
    }

    [Theory]
    [InlineData(RecurringIncomeFrequency.Weekly, 100, 200)] // 100 * 52 / 26
    [InlineData(RecurringIncomeFrequency.Biweekly, 160, 160)]
    [InlineData(RecurringIncomeFrequency.Monthly, 2000, 923.08)] // 2000 * 12 / 26
    [InlineData(RecurringIncomeFrequency.Yearly, 26000, 1000)] // 26000 / 26
    public void ToBiweeklyEquivalent_ProducesExpectedFigure(RecurringIncomeFrequency frequency, decimal amount, decimal expected)
    {
        var result = RecurringIncomeEquivalentCalculator.ToBiweeklyEquivalent(amount, frequency);

        Assert.Equal(expected, Math.Round(result, 2));
    }

    [Fact]
    public void ToMonthlyEquivalent_MonthlyAmount_IsUnchanged()
    {
        Assert.Equal(2000m, RecurringIncomeEquivalentCalculator.ToMonthlyEquivalent(2000m, RecurringIncomeFrequency.Monthly));
    }

    [Fact]
    public void ToBiweeklyEquivalent_BiweeklyAmount_IsUnchanged()
    {
        Assert.Equal(160m, RecurringIncomeEquivalentCalculator.ToBiweeklyEquivalent(160m, RecurringIncomeFrequency.Biweekly));
    }
}
