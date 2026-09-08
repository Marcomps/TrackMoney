using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Abstractions;

public interface IBudgetRepository : IRepository<Budget>
{
    Task<Budget?> GetForCategoryAndMonthAsync(Guid categoryId, int year, int month, CurrencyCode currency, CancellationToken ct = default);

    Task<IReadOnlyList<Budget>> GetForMonthAsync(int year, int month, CancellationToken ct = default);
}
