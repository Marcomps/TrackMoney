using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Repositories;

internal sealed class MedicalExpenseDetailRepository : RepositoryBase<MedicalExpenseDetail>, IMedicalExpenseDetailRepository
{
    public MedicalExpenseDetailRepository(TrackTraceMoneyDbContext context, IDbAccessGate gate) : base(context, gate)
    {
    }

    public Task<MedicalExpenseDetail?> GetForTransactionAsync(Guid transactionId, CancellationToken ct = default) =>
        GuardedAsync(async () => await Context.Set<MedicalExpenseDetail>()
            .FirstOrDefaultAsync(d => d.TransactionId == transactionId, ct), ct);

    public Task<IReadOnlyDictionary<Guid, MedicalExpenseDetail>> GetForTransactionsAsync(IEnumerable<Guid> transactionIds, CancellationToken ct = default)
    {
        var ids = transactionIds.ToList();
        return GuardedAsync<IReadOnlyDictionary<Guid, MedicalExpenseDetail>>(async () =>
        {
            var details = await Context.Set<MedicalExpenseDetail>()
                .Where(d => ids.Contains(d.TransactionId))
                .ToListAsync(ct);
            return details.ToDictionary(d => d.TransactionId);
        }, ct);
    }

    public Task<IReadOnlyList<MedicalExpenseDetail>> GetPendingAsync(CancellationToken ct = default) =>
        GuardedAsync<IReadOnlyList<MedicalExpenseDetail>>(async () => await Context.Set<MedicalExpenseDetail>()
            .Where(d => d.Status == MedicalReimbursementStatus.Pending)
            .ToListAsync(ct), ct);
}
