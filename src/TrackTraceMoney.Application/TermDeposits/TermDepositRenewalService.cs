using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Application.TermDeposits;

/// <summary>
/// Finds every active, auto-renewal-enabled term deposit that has reached (or passed) its maturity date
/// and rolls each one into a fresh <see cref="TermDeposit"/> via <see cref="TermDeposit.RenewAtMaturity"/>,
/// deactivating the matured one in the same atomic transaction — mirroring
/// <c>RecurringExpenseService.ConfirmOccurrenceAsync</c>'s exact BeginTransactionAsync/EnterAmbientScope/
/// SaveChangesAsync/CommitAsync idiom, but per-candidate: each deposit gets its own transaction and its
/// own try/catch so one deposit failing to renew (e.g. a data problem making <c>Balance &lt;= 0</c>)
/// never blocks the rest of the batch from renewing.
/// </summary>
public sealed class TermDepositRenewalService : ITermDepositRenewalService
{
    private readonly IFinancialAccountRepository _financialAccountRepository;
    private readonly IFinancialInstitutionRepository _financialInstitutionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TermDepositRenewalService(
        IFinancialAccountRepository financialAccountRepository,
        IFinancialInstitutionRepository financialInstitutionRepository,
        IUnitOfWork unitOfWork)
    {
        _financialAccountRepository = financialAccountRepository;
        _financialInstitutionRepository = financialInstitutionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TermDepositRenewalResult>> ProcessMaturedRenewalsAsync(
        DateOnly asOfDate, CancellationToken ct = default)
    {
        var accounts = await _financialAccountRepository.GetActiveAsync(ct);
        var candidates = accounts.OfType<TermDeposit>()
            .Where(td => td.AutoRenewal && td.MaturityDate <= asOfDate)
            .ToList();

        var results = new List<TermDepositRenewalResult>();

        foreach (var candidate in candidates)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

            // Must be a plain synchronous call, right here — see IUnitOfWorkTransaction.EnterAmbientScope's
            // doc comment for why delegating this into another awaited method would silently fail to
            // cover the repository calls below.
            using var ambientScope = transaction.EnterAmbientScope();
            try
            {
                var renewal = candidate.RenewAtMaturity(asOfDate);

                // Resolved before AddAsync/SaveChangesAsync/CommitAsync below, deliberately: if this
                // lookup ever fails, nothing must have been persisted yet, so the existing catch/rollback
                // below still means what it says (this candidate's renewal did not happen) instead of
                // rolling back an already-committed transaction. RenewAtMaturity's own defensive guard
                // already rejects a null InstitutionId, so .Value here is safe.
                var institution = await _financialInstitutionRepository.GetByIdAsync(renewal.InstitutionId!.Value, ct);

                await _financialAccountRepository.AddAsync(renewal, ct);
                await _financialAccountRepository.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);

                results.Add(new TermDepositRenewalResult(
                    candidate.Id,
                    renewal.Id,
                    institution?.Name ?? "?",
                    renewal.Currency,
                    renewal.Balance,
                    renewal.MaturityDate));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                // Skip this candidate — one deposit's failure must not abort the rest of the batch.
            }
        }

        return results;
    }
}
