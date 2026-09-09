using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        // Transfer.SourceAccountId MUST get an explicit HasColumnName — TPH siblings with a same-named
        // FK property (here, this type's SourceAccountId and CreditCardPayment.SourceAccountId, added
        // in a later Phase 2 slice) silently split into duplicate shadow columns without it. See this
        // repo's `.claude` memory (EF TPH shared-column gotcha).
        builder.Property(t => t.SourceAccountId).IsRequired().HasColumnName("SourceAccountId");
        builder.Property(t => t.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");

        builder.HasIndex(t => t.SourceAccountId);
        builder.HasIndex(t => t.DestinationAccountId);
    }
}
