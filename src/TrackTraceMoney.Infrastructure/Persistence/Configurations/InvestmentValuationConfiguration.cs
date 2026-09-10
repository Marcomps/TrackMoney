using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class InvestmentValuationConfiguration : IEntityTypeConfiguration<InvestmentValuation>
{
    public void Configure(EntityTypeBuilder<InvestmentValuation> builder)
    {
        builder.ToTable("InvestmentValuations");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.InvestmentFundId).IsRequired();
        builder.Property(v => v.AsOfDate).IsRequired();
        builder.Property(v => v.Value).IsRequired();

        builder.HasIndex(v => new { v.InvestmentFundId, v.AsOfDate }).IsUnique();
    }
}
