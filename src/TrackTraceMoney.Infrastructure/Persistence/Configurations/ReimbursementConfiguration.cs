using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Reimbursement.DestinationAccountId"/> intentionally SHARES the
/// <c>DestinationAccountId</c> column with <see cref="Income"/>/<see cref="Transfer"/>/
/// <see cref="InvestmentContribution"/>/<see cref="InvestmentWithdrawal"/>/<see cref="InterestIncome"/> —
/// same semantic meaning (a receiving <see cref="Domain.Accounts.FinancialAccount"/>). See this repo's
/// `.claude` memory (EF TPH shared-column gotcha).
/// </summary>
public sealed class ReimbursementConfiguration : IEntityTypeConfiguration<Reimbursement>
{
    public void Configure(EntityTypeBuilder<Reimbursement> builder)
    {
        builder.Property(r => r.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");
        builder.Property(r => r.LinkedTransactionId).IsRequired();
        builder.HasIndex(r => r.DestinationAccountId);
        // Non-unique — the domain's one-way Pending-only guard (MedicalExpenseDetail.MarkReimbursed can
        // only run once, since Status flips away from Pending) already prevents duplicate reimbursements
        // against the same linked transaction; no DB-level uniqueness constraint needed on top of it.
        builder.HasIndex(r => r.LinkedTransactionId);
    }
}
