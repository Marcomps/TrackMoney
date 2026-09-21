using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.Application.Abstractions;

public interface IBudgetRepository : IRepository<Budget>
{
    Task<Budget?> GetForCategoryAndMonthAsync(Guid categoryId, int year, int month, CurrencyCode currency, CancellationToken ct = default);

    Task<IReadOnlyList<Budget>> GetForMonthAsync(int year, int month, CancellationToken ct = default);

    /// <summary>
    /// Whether any <see cref="Budget"/> row references <paramref name="categoryId"/> via
    /// <see cref="Budget.CategoryId"/> (Category lifecycle slice §A.3) -- checked before allowing a hard
    /// delete or deactivate of that category.
    /// </summary>
    Task<bool> HasAnyReferencingCategoryAsync(Guid categoryId, CancellationToken ct = default);
}
