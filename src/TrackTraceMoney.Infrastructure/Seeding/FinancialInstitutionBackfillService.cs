using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Institutions;
using TrackTraceMoney.Infrastructure.Persistence;

namespace TrackTraceMoney.Infrastructure.Seeding;

/// <summary>
/// One-time-per-row backfill from the 4 legacy free-text institution fields (<c>CreditCard.Issuer</c>,
/// <c>Loan.Institution</c>, <c>TermDeposit.Institution</c>, <c>InvestmentFund.Institution</c>) into the
/// new, user-managed <see cref="FinancialInstitution"/> list (see the
/// financial-institution-card-network-slice-spec's Decision 2). Called from the same
/// <see cref="TrackTraceMoney.Application.Abstractions.IFinanceDatabaseInitializer"/> hook as
/// <see cref="CategorySeeder"/>/<see cref="PersonSeeder"/>, immediately after both — that hook already
/// runs on every app startup and on every new-profile creation (Local Profiles feature), so it is the
/// correct, single, already-established place for this too.
///
/// Idempotent by construction, not a table-level "already ran" flag: the per-row guard is simply
/// "the new InstitutionId is still null AND the old free-text column is not null" — an already-backfilled
/// row has InstitutionId set (permanently skipped from then on), and a row created after this slice
/// shipped never populates the old free-text column at all (new Add-screens only ever write
/// InstitutionId), so it's permanently skipped for a different reason. Safe to call on every single app
/// startup forever, no separate "ran once" bookkeeping needed.
///
/// Deliberately exact-trimmed-string matching, not fuzzy/case-insensitive: a wrong fuzzy merge (e.g.
/// folding "Banco Agrícola" and "banco agricola" into one row) is not safely reversible — the information
/// "these were originally two different strings" would be destroyed — which is a worse failure mode than
/// the accepted trade-off, a pre-existing near-duplicate becoming two distinct <see cref="FinancialInstitution"/>
/// rows the user can trivially rename/consolidate by hand afterward via the new list screen.
///
/// Distinct names are pooled *across all four* entity types together, not four separate per-type pools —
/// the same typed string on e.g. a CreditCard and a Loan collapses onto one shared
/// <see cref="FinancialInstitution"/> row, matching how a user would expect "the bank I already typed
/// once" to be recognized regardless of which screen they typed it on.
/// </summary>
public static class FinancialInstitutionBackfillService
{
    public static async Task BackfillInstitutionsAsync(TrackTraceMoneyDbContext context, CancellationToken ct = default)
    {
        var creditCards = await context.Set<CreditCard>()
            .Where(c => c.InstitutionId == null && c.Issuer != null)
            .ToListAsync(ct);
        var loans = await context.Set<Loan>()
            .Where(l => l.InstitutionId == null && l.Institution != null)
            .ToListAsync(ct);
        var termDeposits = await context.Set<TermDeposit>()
            .Where(t => t.InstitutionId == null && t.Institution != null)
            .ToListAsync(ct);
        var investmentFunds = await context.Set<InvestmentFund>()
            .Where(f => f.InstitutionId == null && f.Institution != null)
            .ToListAsync(ct);

        if (creditCards.Count == 0 && loans.Count == 0 && termDeposits.Count == 0 && investmentFunds.Count == 0)
            return;

        // One shared pool across all 4 entity types (see remarks). Each free-text column is filtered
        // non-null above, so the null-forgiving `!` below is safe. Ordinal (case-sensitive) comparison,
        // deliberately no trimming-only-no-casing-normalization shortcuts — exact match, full stop.
        var distinctTrimmedNames = creditCards.Select(c => c.Issuer!.Trim())
            .Concat(loans.Select(l => l.Institution!.Trim()))
            .Concat(termDeposits.Select(t => t.Institution!.Trim()))
            .Concat(investmentFunds.Select(f => f.Institution!.Trim()))
            .Distinct(StringComparer.Ordinal);

        var institutionsByName = distinctTrimmedNames.ToDictionary(
            name => name,
            name => new FinancialInstitution(name),
            StringComparer.Ordinal);

        await context.FinancialInstitutions.AddRangeAsync(institutionsByName.Values, ct);

        foreach (var creditCard in creditCards)
            creditCard.SetInstitutionId(institutionsByName[creditCard.Issuer!.Trim()].Id);
        foreach (var loan in loans)
            loan.SetInstitutionId(institutionsByName[loan.Institution!.Trim()].Id);
        foreach (var termDeposit in termDeposits)
            termDeposit.SetInstitutionId(institutionsByName[termDeposit.Institution!.Trim()].Id);
        foreach (var investmentFund in investmentFunds)
            investmentFund.SetInstitutionId(institutionsByName[investmentFund.Institution!.Trim()].Id);

        await context.SaveChangesAsync(ct);
    }
}
