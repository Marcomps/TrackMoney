using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="CreditCardStatement"/> — a standalone entity, not part of the <c>Transaction</c> TPH
/// hierarchy, so no shared-column concerns apply here.
/// </summary>
public sealed class CreditCardStatementConfiguration : IEntityTypeConfiguration<CreditCardStatement>
{
    public void Configure(EntityTypeBuilder<CreditCardStatement> builder)
    {
        builder.ToTable("CreditCardStatements");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CreditAccountId).IsRequired();
        builder.Property(s => s.CycleStartDate).IsRequired();
        builder.Property(s => s.CycleEndDate).IsRequired();
        builder.Property(s => s.MinimumPayment).IsRequired();
        builder.Property(s => s.PayInFullAmount).IsRequired();

        builder.HasIndex(s => new { s.CreditAccountId, s.CycleEndDate }).IsUnique();
    }
}
