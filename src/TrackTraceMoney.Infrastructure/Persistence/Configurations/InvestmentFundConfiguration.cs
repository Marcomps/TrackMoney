using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class InvestmentFundConfiguration : IEntityTypeConfiguration<InvestmentFund>
{
    public void Configure(EntityTypeBuilder<InvestmentFund> builder)
    {
        // Legacy free-text field, now nullable (superseded by InstitutionId) — see InvestmentFund.Institution's
        // own remarks. No longer .IsRequired(): the C# property itself is nullable now. Institution also
        // exists on the sibling TermDeposit subtype in this same TPH hierarchy — pin the column name
        // explicitly on both configurations (see TermDepositConfiguration) or EF splits them into two
        // physical columns (this repo's documented ef-tph-shared-column-gotcha).
        builder.Property(f => f.Institution).HasMaxLength(200).HasColumnName("Institution");

        // InstitutionId is a NEW shared name between TermDeposit/InvestmentFund, introduced by this
        // slice — needs its own separate pin, same reasoning as Institution's above.
        builder.Property(f => f.InstitutionId).HasColumnName("InstitutionId");

        builder.Property(f => f.InvestmentDate).IsRequired();
        builder.Property(f => f.Contributions).IsRequired();
        builder.Property(f => f.Withdrawals).IsRequired();
        builder.Property(f => f.Fees).IsRequired();

        builder.Ignore(f => f.Gain);
        builder.Ignore(f => f.ReturnPercentage);
    }
}
