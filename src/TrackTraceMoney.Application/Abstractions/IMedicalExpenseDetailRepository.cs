using TrackTraceMoney.Domain.MedicalExpenses;

namespace TrackTraceMoney.Application.Abstractions;

public interface IMedicalExpenseDetailRepository : IRepository<MedicalExpenseDetail>
{
    Task<MedicalExpenseDetail?> GetForTransactionAsync(Guid transactionId, CancellationToken ct = default);

    /// <summary>
    /// Batched counterpart to <see cref="GetForTransactionAsync"/> — a single query for every id in
    /// <paramref name="transactionIds"/>, for anti-N+1 use by list screens (History/Transactions)
    /// that would otherwise fetch one row per visible transaction in a loop. Transactions with no
    /// <see cref="MedicalExpenseDetail"/> row are simply omitted from the result, not represented
    /// with a null value.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, MedicalExpenseDetail>> GetForTransactionsAsync(IEnumerable<Guid> transactionIds, CancellationToken ct = default);

    /// <summary>
    /// Every <see cref="MedicalExpenseDetail"/> currently <see cref="Domain.MedicalExpenses.MedicalReimbursementStatus.Pending"/> —
    /// a real <c>WHERE</c>-filtered query, not <see cref="IRepository{TEntity}.GetAllAsync"/> followed by
    /// client-side filtering. Used by <c>AddTransactionViewModel</c>'s Reimbursement block to build its
    /// pending-expense picker without fetching every medical expense detail row in the database.
    /// </summary>
    Task<IReadOnlyList<MedicalExpenseDetail>> GetPendingAsync(CancellationToken ct = default);
}
