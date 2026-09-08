using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.Domain.Tests.RecurringExpenses;

public sealed class RecurringExpenseTests
{
    private static RecurringExpense CreateExpense(
        RecurringExpenseFrequency frequency = RecurringExpenseFrequency.Monthly,
        DateOnly? startDate = null,
        DateOnly? endDate = null) =>
        new(
            "Netflix",
            15m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            frequency,
            startDate ?? new DateOnly(2026, 1, 15),
            endDate);

    [Fact]
    public void Constructor_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringExpense(" ", 10m, Guid.NewGuid(), Guid.NewGuid(), RecurringExpenseFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void Constructor_WithNonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringExpense("Rent", 0m, Guid.NewGuid(), Guid.NewGuid(), RecurringExpenseFrequency.Monthly, new DateOnly(2026, 1, 1), null));
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecurringExpense(
                "Rent",
                10m,
                Guid.NewGuid(),
                Guid.NewGuid(),
                RecurringExpenseFrequency.Monthly,
                new DateOnly(2026, 2, 1),
                new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Constructor_WithEndDateEqualToStartDate_DoesNotThrow()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var expense = new RecurringExpense("Rent", 10m, Guid.NewGuid(), Guid.NewGuid(), RecurringExpenseFrequency.Monthly, startDate, startDate);

        Assert.Equal(startDate, expense.EndDate);
    }

    [Fact]
    public void NextOccurrenceDate_WithNoConfirmations_IsStartDate()
    {
        var startDate = new DateOnly(2026, 1, 15);
        var expense = CreateExpense(startDate: startDate);

        Assert.Equal(startDate, expense.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Weekly_AdvancesSevenDaysFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var expense = CreateExpense(RecurringExpenseFrequency.Weekly, startDate);

        expense.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2026, 1, 8), expense.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Monthly_AdvancesOneMonthFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 1, 15);
        var expense = CreateExpense(RecurringExpenseFrequency.Monthly, startDate);

        expense.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2026, 2, 15), expense.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_Yearly_AdvancesOneYearFromLastConfirmed()
    {
        var startDate = new DateOnly(2026, 3, 10);
        var expense = CreateExpense(RecurringExpenseFrequency.Yearly, startDate);

        expense.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2027, 3, 10), expense.NextOccurrenceDate);
    }

    [Fact]
    public void NextOccurrenceDate_MonthlyFromMonthEndDate_ClampsLikeDateTime()
    {
        // Known, accepted Phase 1 limitation: DateOnly.AddMonths clamps like DateTime, so a
        // recurring expense started on a month-end date drifts earlier over time. Not a bug to fix.
        var startDate = new DateOnly(2026, 1, 31);
        var expense = CreateExpense(RecurringExpenseFrequency.Monthly, startDate);

        expense.MarkConfirmed(startDate);

        Assert.Equal(new DateOnly(2026, 2, 28), expense.NextOccurrenceDate);
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceInFuture_IsFalse()
    {
        var expense = CreateExpense(startDate: new DateOnly(2026, 6, 1));

        Assert.False(expense.IsDue(new DateOnly(2026, 5, 1)));
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsToday_IsTrue()
    {
        var expense = CreateExpense(startDate: new DateOnly(2026, 6, 1));

        Assert.True(expense.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsInThePast_IsTrue()
    {
        var expense = CreateExpense(startDate: new DateOnly(2026, 1, 1));

        Assert.True(expense.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void IsDue_WhenInactive_IsFalseEvenIfOtherwiseDue()
    {
        var expense = CreateExpense(startDate: new DateOnly(2026, 1, 1));
        expense.Deactivate();

        Assert.False(expense.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void IsDue_WhenNextOccurrenceIsAfterEndDate_IsFalse()
    {
        var expense = CreateExpense(startDate: new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 1, 1));

        // First occurrence (== start/end date) is due...
        Assert.True(expense.IsDue(new DateOnly(2026, 1, 1)));

        expense.MarkConfirmed(new DateOnly(2026, 1, 1));

        // ...but the next occurrence would fall after the end date, so it's no longer due.
        Assert.False(expense.IsDue(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public void MarkConfirmed_WithCurrentlyDueOccurrenceDate_UpdatesLastConfirmedDate()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var expense = CreateExpense(startDate: startDate);

        expense.MarkConfirmed(startDate);

        Assert.Equal(startDate, expense.LastConfirmedDate);
    }

    [Fact]
    public void MarkConfirmed_WithDateOtherThanNextOccurrence_Throws()
    {
        var startDate = new DateOnly(2026, 1, 1);
        var expense = CreateExpense(startDate: startDate);

        Assert.Throws<InvalidOperationException>(() => expense.MarkConfirmed(startDate.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ThenReactivate_RestoresIsActive()
    {
        var expense = CreateExpense();

        expense.Deactivate();
        Assert.False(expense.IsActive);

        expense.Reactivate();
        Assert.True(expense.IsActive);
    }
}
