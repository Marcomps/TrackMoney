using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.Application.Abstractions;

public interface ITransactionPresetRepository : IRepository<TransactionPreset>
{
    /// <summary>
    /// Every active <see cref="TransactionPreset"/> -- mirrors <see cref="ICategoryRepository.GetActiveAsync"/>'s
    /// convention. Used by both <c>TransactionPresetsListViewModel</c>'s default (non-"show inactive")
    /// view and <c>AddTransactionViewModel</c>'s "Quick presets" chip row (only active presets are ever
    /// offered as a shortcut).
    /// </summary>
    Task<IReadOnlyList<TransactionPreset>> GetActiveAsync(CancellationToken ct = default);
}
