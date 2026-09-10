using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class InvestmentFundConfiguration : IEntityTypeConfiguration<InvestmentFund>
{
    public void Configure(EntityTypeBuilder<InvestmentFund> builder)
    {
        // Institution also exists on the sibling TermDeposit subtype in this same TPH hierarchy — pin the
        // column name explicitly on both configurations (see TermDepositConfiguration) or EF splits them
        // into two physical columns (this repo's documented ef-tph-shared-column-gotcha).
        builder.Property(f => f.Institution).IsRequired().HasMaxLength(200).HasColumnName("Institution");
        builder.Property(f => f.InvestmentDate).IsRequired();
        builder.Property(f => f.Contributions).IsRequired();
        builder.Property(f => f.Withdrawals).IsRequired();
        builder.Property(f => f.Fees).IsRequired();

        builder.Ignore(f => f.Gain);
        builder.Ignore(f => f.ReturnPercentage);
    }
}
