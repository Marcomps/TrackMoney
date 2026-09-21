using TrackTraceMoney.Domain.Categories;

namespace TrackTraceMoney.Application.Abstractions;

public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>
    /// Every active <see cref="Category"/> -- mirrors <see cref="IFinancialAccountRepository.GetActiveAsync"/>'s
    /// convention, used by <c>CategoriesListViewModel</c>'s default (non-"show inactive") view.
    /// </summary>
    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default);
}
