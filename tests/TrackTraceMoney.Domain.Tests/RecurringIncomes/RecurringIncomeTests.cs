using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.Domain.Tests.RecurringIncomes;

public sealed class RecurringIncomeTests
{
    private static RecurringIncome CreateIncome(
        RecurringIncomeFrequency frequency = RecurringIncomeFrequency.Monthly,
        DateOnly? startDate = null,
        DateOnly? endDate = null) =>
        new(
            "Salary",
            2000m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            frequency,
            startDate ?? new DateOnly(2026, 1, 15),
            endDate);

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringIncome(" ", 10m, Guid.NewGuid(), Guid.NewGuid(), RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringIncome("Salary", 0m, Guid.NewGuid(), Guid.NewGuid(), RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringIncome(
                "Salary",
                10m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                RecurringIncomeFrequency.Monthly,
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Constructor_WithEndDateEqualToStartDate_DoesNotThrow()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var income = new RecurringIncome("Salary", 10m, Guid.NewGuid(), Guid.NewGuid(), RecurringIncomeFrequency.Monthly, startDate, startDate);

        Assert.Equal(startDate, income.EndDate);
    }

    [Fact]
    public void Constructor_SetsDestinationAccountId()
    {
        var accountId = Guid.NewGuid();
        var income = new RecurringIncome("Salary", 2000m, Guid.NewGuid(), accountId, RecurringIncomeFrequency.Monthly, new DateOnly(2026, 1, 15), null);

        Assert.Equal(accountId, income.DestinationAccountId);
    }

    [Fact]
    public void NextOccurrenceDate_WithNoConfirmations_IsStartDate()
    {
        var startDate = new DateOnly(2026, 1, 15);
        var income = CreateIncome(startDate: startDate);

        Assert.Equal(startDate, income.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Weekly_AdvancesSevenDaysFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var income = CreateIncome(RecurringIncomeFrequency.Weekly, startDate);

        income.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2026, 1, 8), income.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Biweekly_AdvancesFourteenDaysFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var income = CreateIncome(RecurringIncomeFrequency.Biweekly, startDate);

        income.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2026, 1, 15), income.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Monthly_AdvancesOneMonthFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 1, 15);
        var income = CreateIncome(RecurringIncomeFrequency.Monthly, startDate);

        income.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2026, 2, 15), income.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Yearly_AdvancesOneYearFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 3, 10);
        var income = CreateIncome(RecurringIncomeFrequency.Yearly, startDate);

        income.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2027, 3, 10), income.NextOccurrenceDate);
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceInFuture_IsFalse()
    {
        var income = CreateIncome(startDate: new DateOnly(2026, 6, 1));

        Assert.False(income.IsDue(new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsToday_IsTrue()
    {
        var income = CreateIncome(startDate: new DateOnly(2026, 6, 1));

        Assert.True(income.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsInThePast_IsTrue()
    {
        var income = CreateIncome(startDate: new DateOnly(2026, 1, 1));

        Assert.True(income.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void IsDue_WhenInactive_IsFalseEvenIfOtherwiseDue()
    {
        var income = CreateIncome(startDate: new DateOnly(2026, 1, 1));
        income.Deactivate();

        Assert.False(income.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsAfterEndDate_IsFalse()
    {
        var income = CreateIncome(startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 1, 1));

        Assert.True(income.IsDue(new DateOnly(2026, 1, 1)));

        income.MarkConfirmed(new DateOnly(2026, 1, 1));

        Assert.False(income.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void CountOccurrencesThrough_Weekly_CountsEveryOccurrenceInHorizonNotJustOne()
    {
        var income = CreateIncome(RecurringIncomeFrequency.Weekly, startDate: new DateOnly(2026, 9, 3));

        Assert.Equal(4, income.CountOccurrencesThrough(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void CountOccurrencesThrough_Monthly_CountsOne()
    {
        var income = CreateIncome(RecurringIncomeFrequency.Monthly, startDate: new DateOnly(2026, 9, 15));

        Assert.Equal(1, income.CountOccurrencesThrough(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void CountOccurrencesThrough_WhenNextOccurrenceInFuture_IsZero()
    {
        var income = CreateIncome(startDate: new DateOnly(2026, 10, 1));

        Assert.Equal(0, income.CountOccurrencesThrough(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void CountOccurrencesThrough_WhenInactive_IsZeroEvenIfOtherwiseDue()
    {
        var income = CreateIncome(RecurringIncomeFrequency.Weekly, startDate: new DateOnly(2026, 9, 1));
        income.Deactivate();

        Assert.Equal(0, income.CountOccurrencesThrough(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void CountOccurrencesThrough_StopsAtEndDateEvenIfHorizonIsLater()
    {
        var income = CreateIncome(
            RecurringIncomeFrequency.Weekly,
            startDate: new DateOnly(2026, 9, 1),
            endDate: new DateOnly(2026, 9, 15));

        Assert.Equal(3, income.CountOccurrencesThrough(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void MarkConfirmed_WithCurrentlyDueOccurrenceDate_UpdatesLastConfirmedDate()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var income = CreateIncome(startDate: startDate);

        income.MarkConfirmed(startDate);

        Assert.Equal(startDate, income.LastConfirmedDate);
    }

    [Fact]
    public void MarkConfirmed_WithDateOtherThanNextOccurrence_Throws()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var income = CreateIncome(startDate: startDate);

        Assert.Throws<InvalidOperationException>(() => income.MarkConfirmed(startDate.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ThenReactivate_RestoresIsActive()
    {
        var income = CreateIncome();

        income.Deactivate();
        Assert.False(income.IsActive);

        income.Reactivate();
        Assert.True(income.IsActive);
    }

    [Fact]
    public void UpdateAmount_WithNonPositiveAmount_Throws()
    {
        var income = CreateIncome();

        Assert.Throws<ArgumentOutOfRangeException>(() => income.UpdateAmount(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => income.UpdateAmount(-5m));
    }

    [Fact]
    public void UpdateAmount_WithPositiveAmount_AppliesImmediately()
    {
        var income = CreateIncome();

        income.UpdateAmount(2500m);

        Assert.Equal(2500m, income.Amount);
    }

    /// <summary>
    /// Prospective-only, per the entity's own doc comment: a raise must never rewrite
    /// LastConfirmedDate or the already-computed NextOccurrenceDate -- only future confirmations
    /// (which read Amount at confirm-time) are affected.
    /// </summary>
    [Fact]
    public void UpdateAmount_DoesNotTouchLastConfirmedDateOrNextOccurrenceDate()
    {
        var startDate = new DateOnly(2026, 1, 15);
        var income = CreateIncome(RecurringIncomeFrequency.Monthly, startDate);
        income.MarkConfirmed(startDate);
        var nextOccurrenceBefore = income.NextOccurrenceDate;

        income.UpdateAmount(2500m);

        Assert.Equal(startDate, income.LastConfirmedDate);
        Assert.Equal(nextOccurrenceBefore, income.NextOccurrenceDate);
    }
}
