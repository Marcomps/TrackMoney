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

    private static List<DateOnly> ConfirmSuccessive(RecurringIncome income, int count)
    {
        var dates = new List<DateOnly>();
        for (var i = 0; i < count; i++)
        {
            var next = income.NextOccurrenceDate;
            dates.Add(next);
            income.MarkConfirmed(next);
        }

        return dates;
    }

    [Fact]
    public void SemiMonthly_PaysOnThe15thAndLastDayOfEachMonth()
    {
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, new DateOnly(2026, 9, 30));

        var dates = ConfirmSuccessive(income, 11);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 31),
                new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 15),
                new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 15), new DateOnly(2027, 1, 31),
                new DateOnly(2027, 2, 15), new DateOnly(2027, 2, 28),
            },
            dates);
    }

    [Fact]
    public void SemiMonthly_CountsTwoOccurrencesPerMonth()
    {
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, new DateOnly(2026, 10, 15));

        Assert.Equal(24, income.CountOccurrencesThrough(new DateOnly(2027, 10, 14)));
    }

    [Fact]
    public void Monthly_OnDay30_ReturnsTo30AfterFebruary_InsteadOfDriftingTo28()
    {
        var income = CreateIncome(RecurringIncomeFrequency.Monthly, new DateOnly(2027, 1, 30));

        var dates = ConfirmSuccessive(income, 4);

        Assert.Equal(
            new[] { new DateOnly(2027, 1, 30), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 30) },
            dates);
    }

    [Fact]
    public void Monthly_OnDay31_LandsOnEachMonthsLastDay()
    {
        var income = CreateIncome(RecurringIncomeFrequency.Monthly, new DateOnly(2026, 8, 31));

        var dates = ConfirmSuccessive(income, 3);

        Assert.Equal(new[] { new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31) }, dates);
    }

    [Theory]
    [InlineData(2026, 9, 30, true)]  // due today
    [InlineData(2026, 9, 23, true)]  // 7 days early -- inside the window
    [InlineData(2026, 9, 22, false)] // 8 days early -- outside
    public void CanConfirm_AllowsUpTo7DaysEarly(int year, int month, int day, bool expected)
    {
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, new DateOnly(2026, 9, 30));

        Assert.Equal(expected, income.CanConfirm(new DateOnly(year, month, day)));
    }

    [Fact]
    public void CanConfirm_AfterEarlyConfirmation_NextPaydayIsOutOfReach()
    {
        // Confirming the 30th on the 28th must not let a second tap confirm the 15th the same day.
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, new DateOnly(2026, 9, 30));
        var today = new DateOnly(2026, 9, 28);

        income.MarkConfirmed(income.NextOccurrenceDate);

        Assert.False(income.CanConfirm(today));
    }

    [Fact]
    public void CanConfirm_OffCycleStart_ConfirmingOnTime_DoesNotExposeNextPaydaySameDay()
    {
        // Started on the 23rd (not a payday): next payday is the 30th, exactly 7 days later.
        var today = new DateOnly(2026, 9, 23);
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, today);

        income.MarkConfirmed(income.NextOccurrenceDate);

        Assert.Equal(new DateOnly(2026, 9, 30), income.NextOccurrenceDate);
        Assert.False(income.CanConfirm(today));
        Assert.True(income.CanConfirm(today.AddDays(1)));
    }

    [Fact]
    public void CanConfirm_WhenInactive_IsFalse()
    {
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, new DateOnly(2026, 9, 30));
        income.Deactivate();

        Assert.False(income.CanConfirm(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void UpdateDetails_ChangesEverything_BeforeAnyConfirmation()
    {
        var income = CreateIncome(RecurringIncomeFrequency.Monthly, new DateOnly(2026, 9, 30));
        var newCategory = Guid.NewGuid();
        var newAccount = Guid.NewGuid();

        income.UpdateDetails(" Sueldo ", 700m, newCategory, newAccount, RecurringIncomeFrequency.SemiMonthly,
            new DateOnly(2026, 10, 15), new DateOnly(2027, 12, 31));

        Assert.Equal("Sueldo", income.Name);
        Assert.Equal(700m, income.Amount);
        Assert.Equal(newCategory, income.CategoryId);
        Assert.Equal(newAccount, income.DestinationAccountId);
        Assert.Equal(RecurringIncomeFrequency.SemiMonthly, income.Frequency);
        Assert.Equal(new DateOnly(2026, 10, 15), income.NextOccurrenceDate);
        Assert.Equal(new DateOnly(2027, 12, 31), income.EndDate);
    }

    [Fact]
    public void UpdateDetails_AfterConfirmation_KeepsHistory_AndRejectsStartDateChange()
    {
        var start = new DateOnly(2026, 9, 30);
        var income = CreateIncome(RecurringIncomeFrequency.SemiMonthly, start);
        income.MarkConfirmed(start);

        Assert.Throws<InvalidOperationException>(() => income.UpdateDetails(
            "Salary", 800m, income.CategoryId, income.DestinationAccountId, income.Frequency, start.AddDays(1), null));

        income.UpdateDetails("Salary", 800m, income.CategoryId, income.DestinationAccountId, income.Frequency, start, null);

        Assert.Equal(800m, income.Amount);
        Assert.Equal(start, income.LastConfirmedDate);
        Assert.Equal(new DateOnly(2026, 10, 15), income.NextOccurrenceDate);
    }

    [Fact]
    public void UpdateDetails_Validates()
    {
        var income = CreateIncome();

        Assert.Throws<ArgumentException>(() => income.UpdateDetails(" ", 1m, Guid.NewGuid(), Guid.NewGuid(), income.Frequency, income.StartDate, null));
        Assert.Throws<ArgumentException>(() => income.UpdateDetails("X", 0m, Guid.NewGuid(), Guid.NewGuid(), income.Frequency, income.StartDate, null));
        Assert.Throws<ArgumentException>(() => income.UpdateDetails("X", 1m, Guid.NewGuid(), Guid.NewGuid(), income.Frequency, income.StartDate, income.StartDate.AddDays(-1)));
    }
}
