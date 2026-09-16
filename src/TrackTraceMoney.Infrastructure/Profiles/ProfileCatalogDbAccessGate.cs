namespace TrackTraceMoney.Infrastructure.Profiles;

/// <summary>
/// Structurally identical to <see cref="Persistence.DbAccessGate"/> (semaphore + <see cref="AsyncLocal{T}"/>
/// ambient-scope tracking) — see <see cref="IProfileCatalogDbAccessGate"/>'s remarks for why this is a
/// separate implementation/instance rather than a reused one.
/// </summary>
internal sealed class ProfileCatalogDbAccessGate : IProfileCatalogDbAccessGate
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

    public IDisposable Acquire()
    {
        if (_ambientlyHeld.Value)
            return NoopScope.Instance;

        _semaphore.Wait();
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
