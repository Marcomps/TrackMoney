namespace TrackTraceMoney.Application.TransactionPresets;

/// <summary>
/// Orchestrates the hard-delete/deactivate/reactivate lifecycle for a
/// <see cref="Domain.TransactionPresets.TransactionPreset"/> (Transaction Type Customization slice spec
/// §B.6/§B.7.5), mirroring <see cref="Categories.ICategoryLifecycleService"/>'s shape structurally. Unlike
/// Category (referenced by six other tables), a <see cref="Domain.TransactionPresets.TransactionPreset"/>
/// never appears on a saved <c>Transaction</c> row -- it only pre-fills fields at entry time -- so there is
/// no reference count to check the way Category/Account do, and <see cref="CanHardDeleteAsync"/> is
/// expected to almost always return <see langword="true"/>.
/// </summary>
public interface ITransactionPresetLifecycleService
{
    /// <summary>
    /// Always <see langword="true"/> today -- no entity in this codebase holds a foreign key back to a
    /// <see cref="Domain.TransactionPresets.TransactionPreset"/> (it is purely an App/Application-layer
    /// convenience, never saved onto a <c>Transaction</c> row). Kept as an explicit async method (rather
    /// than a bare "always allowed" shortcut in the caller) so the guard can start doing something real
    /// the moment a future feature introduces a genuine reference, without changing this interface's shape.
    /// </summary>
    Task<bool> CanHardDeleteAsync(Guid presetId, CancellationToken ct = default);

    Task DeleteAsync(Guid presetId, CancellationToken ct = default);

    Task DeactivateAsync(Guid presetId, CancellationToken ct = default);

    Task ReactivateAsync(Guid presetId, CancellationToken ct = default);
}
