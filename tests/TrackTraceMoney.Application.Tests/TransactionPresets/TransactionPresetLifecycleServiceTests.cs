using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Application.TransactionPresets;
using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.Application.Tests.TransactionPresets;

/// <summary>
/// Covers <see cref="TransactionPresetLifecycleService"/> (Transaction Type Customization slice spec
/// §B.6/§B.7.5): the "almost always true" hard-delete guard (pinned down explicitly, including the
/// card-backed case -- a posted <c>CreditCardPurchase</c> has no foreign key back to the preset that
/// pre-filled it), Deactivate/Reactivate round-trip, DeleteAsync success.
/// </summary>
public sealed class TransactionPresetLifecycleServiceTests
{
    private static (TransactionPresetLifecycleService Service, InMemoryTransactionPresetRepository Presets) CreateSut()
    {
        var presets = new InMemoryTransactionPresetRepository();
        var service = new TransactionPresetLifecycleService(presets);
        return (service, presets);
    }

    [Fact]
    public async Task CanHardDeleteAsync_FreshlyCreatedPreset_ReturnsTrue()
    {
        var (service, presets) = CreateSut();
        var preset = presets.Add(new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null));

        Assert.True(await service.CanHardDeleteAsync(preset.Id));
    }

    /// <summary>
    /// Pinned down explicitly per §B.7.5 -- a card-backed preset's hard-delete guard is exactly as
    /// near-trivial as the plain case, since the resulting CreditCardPurchase row has no foreign key back
    /// to the preset that pre-filled it. Nothing here simulates "the preset was used" (there is no such
    /// state to simulate -- a preset is stateless with respect to transactions it once pre-filled), which
    /// is itself the point: there is no spurious CreditAccountId-based reference check to accidentally
    /// wire up.
    /// </summary>
    [Fact]
    public async Task CanHardDeleteAsync_CardBackedPreset_ReturnsTrue()
    {
        var (service, presets) = CreateSut();
        var preset = presets.Add(new TransactionPreset(
            "Gasolina", null, TransactionPresetBaseType.Expense, null, null, Guid.NewGuid()));

        Assert.True(await service.CanHardDeleteAsync(preset.Id));
    }

    [Fact]
    public async Task DeleteAsync_RemovesPreset()
    {
        var (service, presets) = CreateSut();
        var preset = presets.Add(new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null));

        await service.DeleteAsync(preset.Id);

        Assert.Null(await presets.GetByIdAsync(preset.Id));
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse()
    {
        var (service, presets) = CreateSut();
        var preset = presets.Add(new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null));

        await service.DeactivateAsync(preset.Id);

        Assert.False(preset.IsActive);
    }

    [Fact]
    public async Task ReactivateAsync_SetsIsActiveTrue()
    {
        var (service, presets) = CreateSut();
        var preset = presets.Add(new TransactionPreset("Gasolina", null, TransactionPresetBaseType.Expense, null, null, null));
        preset.Deactivate();

        await service.ReactivateAsync(preset.Id);

        Assert.True(preset.IsActive);
    }

    private sealed class InMemoryTransactionPresetRepository : ITransactionPresetRepository
    {
        private readonly Dictionary<Guid, TransactionPreset> _presets = new();

        public TransactionPreset Add(TransactionPreset preset)
        {
            _presets[preset.Id] = preset;
            return preset;
        }

        public Task<TransactionPreset?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_presets.GetValueOrDefault(id));

        public Task<IReadOnlyList<TransactionPreset>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TransactionPreset>>(_presets.Values.ToList());

        public Task<IReadOnlyList<TransactionPreset>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TransactionPreset>>(_presets.Values.Where(p => p.IsActive).ToList());

        public Task AddAsync(TransactionPreset entity, CancellationToken ct = default)
        {
            _presets[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public void Remove(TransactionPreset entity) => _presets.Remove(entity.Id);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
