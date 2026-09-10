using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class TermDepositConfiguration : IEntityTypeConfiguration<TermDeposit>
{
    public void Configure(EntityTypeBuilder<TermDeposit> builder)
    {
        // Institution also exists on the sibling InvestmentFund subtype in this same TPH hierarchy — pin
        // the column name explicitly here (and on InvestmentFundConfiguration) or EF splits them into two
        // physical columns (this repo's documented ef-tph-shared-column-gotcha).
        builder.Property(t => t.Institution).IsRequired().HasMaxLength(200).HasColumnName("Institution");
        builder.Property(t => t.InitialPrincipal).IsRequired();
        builder.Property(t => t.Rate).IsRequired();
        builder.Property(t => t.RateType).HasConversion<string>().IsRequired();
        builder.Property(t => t.StartDate).IsRequired();
        builder.Property(t => t.MaturityDate).IsRequired();
        builder.Property(t => t.InterestFrequency).HasConversion<string>().IsRequired();
        builder.Property(t => t.IsCompounding).IsRequired();
        builder.Property(t => t.EstimatedInterest);
        builder.Property(t => t.InterestReceived).IsRequired();
        builder.Property(t => t.AutoRenewal).IsRequired();
    }
}
