using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.Property(t => t.SourceAccountId).IsRequired();
        builder.Property(t => t.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");

        builder.HasIndex(t => t.SourceAccountId);
        builder.HasIndex(t => t.DestinationAccountId);
    }
}
