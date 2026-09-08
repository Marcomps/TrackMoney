using Microsoft.EntityFrameworkCore.Storage;
using TrackTraceMoney.Application.Abstractions;

namespace TrackTraceMoney.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed <see cref="IUnitOfWork"/>. Relies on this app's DI setup resolving a single,
/// long-lived <see cref="TrackTraceMoneyDbContext"/> instance for the whole session (see
/// <see cref="LocalBackupService"/>'s remarks for the underlying investigation) so that a transaction
/// begun here is the ambient transaction for every repository's subsequent <c>SaveChangesAsync</c>
/// call, regardless of which repository instance issues it.
///
/// Also owns the <see cref="IDbAccessGate"/> exclusivity for the whole transaction span: acquired when
/// the transaction begins, released only when it is committed/rolled back and disposed, so no unrelated
/// concurrent operation (e.g. a Dashboard load triggered by navigation mid-transaction) can interleave
/// with — or get silently pulled into — this transaction. See <see cref="DbAccessGate"/>'s remarks for
/// why repository calls made *inside* this transaction must go through
/// <see cref="IUnitOfWorkTransaction.EnterAmbientScope"/> rather than re-acquiring the gate themselves.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly TrackTraceMoneyDbContext _dbContext;
    private readonly IDbAccessGate _gate;

    public UnitOfWork(TrackTraceMoneyDbContext dbContext, IDbAccessGate gate)
    {
        _dbContext = dbContext;
        _gate = gate;
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var gateScope = await _gate.AcquireAsync(ct);
        try
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            return new EfCoreUnitOfWorkTransaction(transaction, gateScope, _gate);
        }
        catch
        {
            gateScope.Dispose();
            throw;
        }
    }

    private sealed class EfCoreUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly IDisposable _gateScope;
        private readonly IDbAccessGate _gate;
        private bool _disposed;

        public EfCoreUnitOfWorkTransaction(IDbContextTransaction transaction, IDisposable gateScope, IDbAccessGate gate)
        {
            _transaction = transaction;
            _gateScope = gateScope;
            _gate = gate;
        }

        public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);

        public IDisposable EnterAmbientScope() => _gate.EnterAmbientScope();

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await _transaction.DisposeAsync();
            _gateScope.Dispose();
        }
    }
}
