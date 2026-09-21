using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.TransactionPresets;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class TransactionPresetConfiguration : IEntityTypeConfiguration<TransactionPreset>
{
    public void Configure(EntityTypeBuilder<TransactionPreset> builder)
    {
        builder.ToTable("TransactionPresets");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Icon).HasMaxLength(50);
        builder.Property(p => p.BaseType).HasConversion<string>().IsRequired();
        builder.Property(p => p.DefaultCategoryId);
        builder.Property(p => p.DefaultAccountId);
        builder.Property(p => p.DefaultCreditAccountId);
        builder.Property(p => p.IsActive).IsRequired();

        builder.HasIndex(p => p.IsActive);
    }
}
