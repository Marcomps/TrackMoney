using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.Application.Categories;

public sealed class CategoryLifecycleService : ICategoryLifecycleService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringExpenseRepository _recurringExpenseRepository;
    private readonly IRecurringIncomeRepository _recurringIncomeRepository;
    private readonly IBudgetRepository _budgetRepository;

    public CategoryLifecycleService(
        ICategoryRepository categoryRepository,
        ITransactionRepository transactionRepository,
        IRecurringExpenseRepository recurringExpenseRepository,
        IRecurringIncomeRepository recurringIncomeRepository,
        IBudgetRepository budgetRepository)
    {
        _categoryRepository = categoryRepository;
        _transactionRepository = transactionRepository;
        _recurringExpenseRepository = recurringExpenseRepository;
        _recurringIncomeRepository = recurringIncomeRepository;
        _budgetRepository = budgetRepository;
    }

    public async Task<bool> CanHardDeleteAsync(Guid categoryId, CancellationToken ct = default)
    {
        if (await _transactionRepository.HasAnyTransactionReferencingCategoryAsync(categoryId, ct))
            return false;

        if (await _recurringExpenseRepository.HasAnyReferencingCategoryAsync(categoryId, ct))
            return false;

        if (await _recurringIncomeRepository.HasAnyReferencingCategoryAsync(categoryId, ct))
            return false;

        if (await _budgetRepository.HasAnyReferencingCategoryAsync(categoryId, ct))
            return false;

        return true;
    }

    public async Task DeleteAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct)
            ?? throw new InvalidOperationException($"Category '{categoryId}' was not found.");

        if (category.IsSystemDefined)
            throw new InvalidOperationException("System-defined categories cannot be deleted.");

        // Never trust a caller's cached "yes" -- re-check immediately before deleting.
        if (!await CanHardDeleteAsync(categoryId, ct))
            throw new InvalidOperationException(
                $"Category '{categoryId}' cannot be hard-deleted; it is still referenced by a transaction, recurring definition, or budget.");

        _categoryRepository.Remove(category);
        await _categoryRepository.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct)
            ?? throw new InvalidOperationException($"Category '{categoryId}' was not found.");

        if (category.IsSystemDefined)
            throw new InvalidOperationException("System-defined categories cannot be deactivated.");

        category.Deactivate();
        await _categoryRepository.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid categoryId, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct)
            ?? throw new InvalidOperationException($"Category '{categoryId}' was not found.");

        category.Reactivate();
        await _categoryRepository.SaveChangesAsync(ct);
    }
}
