using TrackTraceMoney.Domain.Common;

namespace TrackTraceMoney.Domain.Budgets;

/// <summary>One category's spending limit for one calendar month (README §34).</summary>
public sealed class Budget : Entity
{
    public Guid CategoryId { get; private set; }

    public decimal Amount { get; private set; }

    public int Year { get; private set; }

    public int Month { get; private set; }

    private Budget()
    {
    }

    public Budget(Guid categoryId, decimal amount, int year, int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");

        CategoryId = categoryId;
        Year = year;
        Month = month;
        UpdateAmount(amount);
    }

    public void UpdateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Budget amount must be positive.");

        Amount = amount;
    }
}
