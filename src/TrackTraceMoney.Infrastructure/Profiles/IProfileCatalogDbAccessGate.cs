namespace TrackTraceMoney.Infrastructure.Profiles;

/// <summary>
/// Serializes access to the app's single, long-lived <see cref="ProfileCatalogDbContext"/> instance —
/// structurally identical to <see cref="Persistence.IDbAccessGate"/>/<see cref="Persistence.DbAccessGate"/>
/// (see that type's remarks for the full investigation: this app's DI setup resolves DbContext types as
/// singletons in practice because MAUI Shell never creates a per-navigation <c>IServiceScope</c>, so EF
/// Core's own reentrancy guard against concurrent operations on one <c>DbContext</c> instance must be
/// enforced explicitly at the call site via a semaphore).
///
/// <para>
/// Deliberately a SEPARATE gate instance from <see cref="Persistence.IDbAccessGate"/>, not a reused one
/// — the two guard two physically separate SQLite databases (this small, always-open profile catalog
/// vs. the per-profile finance database), so serializing them against the same semaphore would block
/// unrelated operations (e.g. a profile-list read blocking on an in-flight, unrelated finance-database
/// query) for no correctness benefit. Register both as independent singletons.
/// </para>
/// </summary>
public interface IProfileCatalogDbAccessGate
{
    /// <summary>
    /// Waits for exclusive access to the shared <see cref="ProfileCatalogDbContext"/> and returns a
    /// scope that must be disposed once the caller's single database operation has fully completed. If
    /// the current async call chain already holds access via <see cref="EnterAmbientScope"/>, this
    /// returns immediately without waiting again.
    /// </summary>
    Task<IDisposable> AcquireAsync(CancellationToken ct = default);

    /// <summary>
    /// Synchronous counterpart to <see cref="AcquireAsync"/>, for the rare caller that must stay
    /// synchronous (mirrors <see cref="Persistence.IDbAccessGate.Acquire"/> — see its remarks). Honors
    /// the same ambient-scope short-circuit as <see cref="AcquireAsync"/>.
    /// </summary>
    IDisposable Acquire();

    /// <summary>
    /// Marks the remainder of the current synchronous continuation — and everything it subsequently
    /// awaits — as already holding this gate, so nested calls to <see cref="AcquireAsync"/> made from
    /// within that continuation return immediately instead of deadlocking against an outer acquisition.
    /// Mirrors <see cref="Persistence.IDbAccessGate.EnterAmbientScope"/> — see its remarks for why this
    /// must be called as a plain synchronous statement, not delegated to another <c>async</c> method.
    /// </summary>
    IDisposable EnterAmbientScope();
}
