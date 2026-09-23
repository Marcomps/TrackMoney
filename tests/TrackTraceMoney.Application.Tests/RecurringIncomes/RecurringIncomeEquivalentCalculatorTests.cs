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
    [InlineData(RecurringIncomeFrequency.Weekly, 100, 216.67)] // 100 * 52 / 12 / 2
    [InlineData(RecurringIncomeFrequency.Biweekly, 160, 173.33)] // 160 * 26 / 12 / 2
    [InlineData(RecurringIncomeFrequency.SemiMonthly, 654.52, 654.52)]
    [InlineData(RecurringIncomeFrequency.Monthly, 1309.04, 654.52)]
    [InlineData(RecurringIncomeFrequency.Yearly, 24000, 1000)] // 24000 / 24
    public void ToSemiMonthlyEquivalent_IsHalfTheMonthlyEquivalent(RecurringIncomeFrequency frequency, decimal amount, decimal expected)
    {
        var result = RecurringIncomeEquivalentCalculator.ToSemiMonthlyEquivalent(amount, frequency);

        Assert.Equal(expected, Math.Round(result, 2));
    }

    [Fact]
    public void ToMonthlyEquivalent_SemiMonthlyPaycheck_IsTwoPaychecks_NotTwentySixOverTwelve()
    {
        // A "quincenal" net pay of 654.52 is 1,309.04 a month -- not 1,418.13 (the every-14-days figure).
        Assert.Equal(1309.04m, RecurringIncomeEquivalentCalculator.ToMonthlyEquivalent(654.52m, RecurringIncomeFrequency.SemiMonthly));
    }

    [Fact]
    public void ToMonthlyEquivalent_MonthlyAmount_IsUnchanged()
    {
        Assert.Equal(2000m, RecurringIncomeEquivalentCalculator.ToMonthlyEquivalent(2000m, RecurringIncomeFrequency.Monthly));
    }
}
