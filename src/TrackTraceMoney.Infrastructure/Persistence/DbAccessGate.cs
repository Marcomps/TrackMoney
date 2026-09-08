namespace TrackTraceMoney.Infrastructure.Persistence;

/// <summary>
/// Serializes access to the app's single, long-lived <see cref="TrackTraceMoneyDbContext"/> instance
/// so concurrent navigation (e.g. History triggers a load, the user switches to Dashboard before it
/// finishes, and Dashboard's <c>OnAppearing</c> fires a second load) can no longer race two operations
/// against the same non-thread-safe <see cref="TrackTraceMoneyDbContext"/> instance and throw
/// <c>InvalidOperationException</c> ("A second operation was started on this context instance before
/// a previous operation completed").
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists (see <see cref="LocalBackupService"/>'s remarks for the full DI investigation
/// this builds on):</b> this app's DI setup resolves the Scoped <see cref="TrackTraceMoneyDbContext"/>
/// directly from the root <see cref="IServiceProvider"/> (MAUI Shell has no per-navigation scope), so
/// in practice there is one context instance for the whole app session, shared by every page.
/// Introducing genuine per-operation <see cref="TrackTraceMoneyDbContext"/> instances (e.g. via
/// <c>IDbContextFactory</c>) would ripple through all seven repositories and every ViewModel
/// constructor and — more importantly — would break <see cref="UnitOfWork"/>:
/// <c>RecurringExpenseService.ConfirmOccurrenceAsync</c> begins an EF Core transaction on the
/// assumption that every repository used inside it shares the exact same context/connection so they
/// enlist in the same ambient transaction. A targeted mitigation that keeps the single shared instance
/// but serializes access to it preserves that invariant by construction — there is still exactly one
/// context, so "same context" is trivially still true.
/// </para>
/// <para>
/// <b>Why a call-site semaphore instead of an EF Core connection interceptor:</b> an earlier version of
/// this fix tried gating at the ADO.NET connection-open/close level via a
/// <c>DbConnectionInterceptor</c>, reasoning that EF Core's SQLite provider opens the connection once
/// per operation (or once for the whole span of an explicit transaction) and that gating around that
/// would need no repository changes at all. That was disproven empirically: EF Core's own reentrancy
/// guard (the thing that actually throws the "second operation" exception) is tracked directly on the
/// <see cref="TrackTraceMoneyDbContext"/> instance and is checked independently of connection state —
/// delaying a second caller inside a connection interceptor still left both operations "in flight"
/// simultaneously from EF Core's point of view, and in practice made the crash easier to hit, not
/// harder. The only design that reliably prevented the crash in spike testing was acquiring the gate
/// <i>before issuing the call into <see cref="TrackTraceMoneyDbContext"/> at all</i> and holding it
/// until that call's own <c>await</c> fully completes — i.e. gating at the repository call site, not
/// at the ADO.NET connection level. That is what this class (via <see cref="RepositoryBase{TEntity}"/>)
/// does.
/// </para>
/// <para>
/// <b>Reentrancy for <see cref="UnitOfWork"/>'s transaction:</b> <c>RecurringExpenseService.
/// ConfirmOccurrenceAsync</c> begins an explicit transaction and then makes several repository calls
/// "inside" it; those calls must not separately wait on this same gate (the transaction's own
/// <see cref="AcquireAsync"/> call already holds it) or the operation deadlocks — the transaction's own
/// disposal, which is what releases the gate, cannot run until those nested calls finish. A naive
/// solution would try to detect "am I already inside a transaction on this context" automatically via
/// <see cref="AsyncLocal{T}"/>, but that does not work here: <see cref="AsyncLocal{T}"/> mutations made
/// inside an <c>await</c>ed callee (e.g. inside <c>UnitOfWork.BeginTransactionAsync</c>'s own method
/// body, whether before or after its own first <c>await</c>) are never visible back to the caller once
/// that callee returns — confirmed empirically before writing this class, since it is easy to assume
/// otherwise. <see cref="AsyncLocal{T}"/> only flows <i>forward</i>, into things a method calls
/// afterward within its own continuation — never backward, out through an <c>await</c> boundary to the
/// method that called it.
/// </para>
/// <para>
/// The fix is <see cref="EnterAmbientScope"/>: a plain, non-<c>async</c> method (see
/// <c>UnitOfWork.EfCoreUnitOfWorkTransaction.EnterAmbientScope</c>) that
/// <c>RecurringExpenseService.ConfirmOccurrenceAsync</c> must call itself, directly, as a synchronous
/// statement in its own method body, immediately after obtaining the transaction (not tucked inside
/// another awaited helper). A plain synchronous call never forks <see cref="ExecutionContext"/> — only
/// actual <c>async</c>/<c>await</c> state-machine boundaries do — so the <see cref="AsyncLocal{T}"/>
/// mutation it performs behaves exactly as if it were inlined into the caller's own code, and
/// correctly flows into every repository call the caller subsequently awaits. Unrelated, independent
/// call chains (e.g. Dashboard's own load, which never goes anywhere near
/// <see cref="EnterAmbientScope"/>) never observe this flag as set, so they still correctly wait on the
/// real semaphore instead of racing ahead — this only elides the wait for the one call chain that
/// legitimately already holds it.
/// </para>
/// </remarks>
public interface IDbAccessGate
{
    /// <summary>
    /// Waits for exclusive access to the shared <see cref="TrackTraceMoneyDbContext"/> and returns a
    /// scope that must be disposed once the caller's single database operation has fully completed.
    /// If the current async call chain already holds access via <see cref="EnterAmbientScope"/> (i.e.
    /// this call is happening "inside" an <see cref="UnitOfWork"/> transaction that already acquired
    /// the gate), this returns immediately without waiting again.
    /// </summary>
    Task<IDisposable> AcquireAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks the remainder of the current synchronous continuation — and everything it subsequently
    /// awaits — as already holding this gate, so nested calls to <see cref="AcquireAsync"/> made from
    /// within that continuation return immediately instead of deadlocking against an outer acquisition
    /// that only releases once those nested calls finish. See this interface's containing type's
    /// remarks for why this must be called as a plain synchronous statement, not delegated to another
    /// <c>async</c> method. The caller must dispose the returned scope once its own transaction/logical
    /// unit of work completes.
    /// </summary>
    IDisposable EnterAmbientScope();
}

internal sealed class DbAccessGate : IDbAccessGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<bool> _ambientlyHeld = new();

    public async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        if (_ambientlyHeld.Value)
            return NoopScope.Instance;

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        return new SemaphoreReleaseScope(_semaphore);
    }

    public IDisposable EnterAmbientScope()
    {
        var previousValue = _ambientlyHeld.Value;
        _ambientlyHeld.Value = true;
        return new AmbientScopeExit(_ambientlyHeld, previousValue);
    }

    private sealed class SemaphoreReleaseScope : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public SemaphoreReleaseScope(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            _semaphore.Release();
        }
    }

    private sealed class AmbientScopeExit : IDisposable
    {
        private readonly AsyncLocal<bool> _ambientlyHeld;
        private readonly bool _previousValue;
        private bool _exited;

        public AmbientScopeExit(AsyncLocal<bool> ambientlyHeld, bool previousValue)
        {
            _ambientlyHeld = ambientlyHeld;
            _previousValue = previousValue;
        }

        public void Dispose()
        {
            if (_exited)
                return;

            _exited = true;
            _ambientlyHeld.Value = _previousValue;
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
