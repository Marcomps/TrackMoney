using TrackTraceMoney.Application.RecurringIncomes;
using TrackTraceMoney.Domain.RecurringIncomes;

namespace TrackTraceMoney.App.Models;

/// <summary>A row in the full recurring-income-definitions list (recurring-income slice spec §4).</summary>
public sealed record RecurringIncomeListItem(
    Guid Id,
    string Name,
    decimal Amount,
    string CategoryName,
    string AccountName,
    RecurringIncomeFrequency Frequency,
    DateOnly NextDueDate,
    bool IsActive,
    decimal MonthlyEquivalent,
    decimal SemiMonthlyEquivalent)
{
    public static RecurringIncomeListItem FromDomain(
        RecurringIncome recurringIncome,
        string categoryName,
        string accountName) =>
        new(
            recurringIncome.Id,
            recurringIncome.Name,
            recurringIncome.Amount,
            categoryName,
            accountName,
            recurringIncome.Frequency,
            recurringIncome.NextOccurrenceDate,
            recurringIncome.IsActive,
            RecurringIncomeEquivalentCalculator.ToMonthlyEquivalent(recurringIncome.Amount, recurringIncome.Frequency),
            RecurringIncomeEquivalentCalculator.ToSemiMonthlyEquivalent(recurringIncome.Amount, recurringIncome.Frequency));
}
