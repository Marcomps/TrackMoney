using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.Application.TransactionPresets;

public sealed class TransactionPresetLifecycleService : ITransactionPresetLifecycleService
{
    private readonly ITransactionPresetRepository _transactionPresetRepository;

    public TransactionPresetLifecycleService(ITransactionPresetRepository transactionPresetRepository)
    {
        _transactionPresetRepository = transactionPresetRepository;
    }

    /// <summary>Always true -- see the interface's own remarks for why no reference-count check exists
    /// here (a <see cref="Domain.TransactionPresets.TransactionPreset"/> is never saved onto a
    /// <c>Transaction</c> row, so there is nothing in this codebase that could ever reference one).</summary>
    public Task<bool> CanHardDeleteAsync(Guid presetId, CancellationToken ct = default) => Task.FromResult(true);

    public async Task DeleteAsync(Guid presetId, CancellationToken ct = default)
    {
        var preset = await _transactionPresetRepository.GetByIdAsync(presetId, ct)
            ?? throw new InvalidOperationException($"Transaction preset '{presetId}' was not found.");

        // Never trust a caller's cached "yes" -- re-check immediately before deleting, mirroring
        // CategoryLifecycleService.DeleteAsync's precedent, even though this guard is expected to
        // essentially never block for this entity.
        if (!await CanHardDeleteAsync(presetId, ct))
            throw new InvalidOperationException($"Transaction preset '{presetId}' cannot be hard-deleted.");

        _transactionPresetRepository.Remove(preset);
        await _transactionPresetRepository.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid presetId, CancellationToken ct = default)
    {
        var preset = await _transactionPresetRepository.GetByIdAsync(presetId, ct)
            ?? throw new InvalidOperationException($"Transaction preset '{presetId}' was not found.");

        preset.Deactivate();
        await _transactionPresetRepository.SaveChangesAsync(ct);
    }

    public async Task ReactivateAsync(Guid presetId, CancellationToken ct = default)
    {
        var preset = await _transactionPresetRepository.GetByIdAsync(presetId, ct)
            ?? throw new InvalidOperationException($"Transaction preset '{presetId}' was not found.");

        preset.Reactivate();
        await _transactionPresetRepository.SaveChangesAsync(ct);
    }
}
