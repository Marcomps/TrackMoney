using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.Property(i => i.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");
        builder.Property(i => i.CategoryId).IsRequired().HasColumnName("CategoryId");

        builder.HasIndex(i => i.DestinationAccountId);
        builder.HasIndex(i => i.CategoryId);
    }
}
