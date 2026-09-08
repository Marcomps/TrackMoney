namespace TrackTraceMoney.Application.Abstractions;

/// <summary>
/// Abstracts an explicit database transaction so an Application-layer service can group multiple
/// repository <c>SaveChangesAsync</c> calls (potentially spanning more than one repository/service)
/// into a single atomic commit, without the Application layer taking a direct dependency on EF Core
/// or any other Infrastructure-layer concern (reference direction: Application -> Domain only).
///
/// Only meaningful when the participating repositories share the same underlying database
/// connection/context instance — see <c>RecurringExpenseService.ConfirmOccurrenceAsync</c> for the
/// motivating case (posting an <c>Expense</c> and marking a recurring expense confirmed must commit
/// or roll back together).
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// An in-flight database transaction. Call <see cref="CommitAsync"/> once every operation inside the
/// transaction has succeeded. If it is disposed without having been committed (e.g. because an
/// exception propagated out of the surrounding <c>try</c> block), the underlying transaction must be
/// rolled back so no partial state is ever persisted.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// Marks every repository call made for the remainder of the current method — and everything it
    /// subsequently awaits — as covered by this transaction's already-acquired exclusive access to the
    /// shared database connection, so those calls do not themselves wait (and deadlock) trying to
    /// acquire it a second time.
    ///
    /// <para>
    /// <b>Must be called as a plain, synchronous statement directly in the method that obtained this
    /// transaction</b> (e.g. immediately after <c>await unitOfWork.BeginTransactionAsync(ct)</c>), not
    /// awaited away or delegated into another <c>async</c> helper method. This ambient marking only
    /// flows forward into code called <i>after</i> the point where it is set, within the same
    /// synchronous continuation — it does not propagate back out through an <c>await</c> boundary to
    /// whatever called this method. Dispose the returned scope once this transaction's logical unit of
    /// work is complete (a <c>using</c> block is the natural fit).
    /// </para>
    /// </summary>
    IDisposable EnterAmbientScope();
}
