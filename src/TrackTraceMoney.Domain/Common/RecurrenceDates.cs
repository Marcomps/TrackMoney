namespace TrackTraceMoney.Domain.Common;

/// <summary>
/// Date arithmetic shared by recurring definitions (<c>RecurringIncome</c>, <c>RecurringExpense</c>).
/// </summary>
public static class RecurrenceDates
{
    /// <summary>
    /// Moves <paramref name="from"/> forward by <paramref name="months"/>, landing on
    /// <paramref name="anchorDay"/> clamped to the target month's length. Anchoring to the definition's
    /// original day (rather than chaining <see cref="DateOnly.AddMonths"/> off the previous occurrence)
    /// keeps a "day 30" schedule from permanently drifting to the 28th after passing through February.
    /// </summary>
    public static DateOnly AddMonthsAnchored(DateOnly from, int months, int anchorDay)
    {
        var target = from.AddMonths(months);
        var day = Math.Min(anchorDay, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateOnly(target.Year, target.Month, day);
    }

    /// <summary>
    /// The next semi-monthly payday strictly after <paramref name="after"/>: the 15th, then the last
    /// day of the month, then the 15th of the following month.
    /// </summary>
    public static DateOnly NextSemiMonthlyPayday(DateOnly after)
    {
        var lastDay = DateTime.DaysInMonth(after.Year, after.Month);

        if (after.Day < 15)
            return new DateOnly(after.Year, after.Month, 15);

        if (after.Day < lastDay)
            return new DateOnly(after.Year, after.Month, lastDay);

        var nextMonth = after.AddMonths(1);
        return new DateOnly(nextMonth.Year, nextMonth.Month, 15);
    }
}
