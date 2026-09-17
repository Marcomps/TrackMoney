using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class TermDepositConfiguration : IEntityTypeConfiguration<TermDeposit>
{
    public void Configure(EntityTypeBuilder<TermDeposit> builder)
    {
        // Legacy free-text field, now nullable (superseded by InstitutionId) — see TermDeposit.Institution's
        // own remarks. No longer .IsRequired(): the C# property itself is nullable now. Institution also
        // exists on the sibling InvestmentFund subtype in this same TPH hierarchy — still pin the column
        // name explicitly here (and on InvestmentFundConfiguration) or EF splits them into two physical
        // columns (this repo's documented ef-tph-shared-column-gotcha).
        builder.Property(t => t.Institution).HasMaxLength(200).HasColumnName("Institution");

        // InstitutionId is a NEW shared name between TermDeposit/InvestmentFund, introduced by this
        // slice — needs its own separate pin, same reasoning as Institution's above.
        builder.Property(t => t.InstitutionId).HasColumnName("InstitutionId");

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
