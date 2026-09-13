using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.NetWorth;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class NetWorthSnapshotConfiguration : IEntityTypeConfiguration<NetWorthSnapshot>
{
    public void Configure(EntityTypeBuilder<NetWorthSnapshot> builder)
    {
        builder.ToTable("NetWorthSnapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Currency).HasConversion<string>().IsRequired();
        builder.Property(s => s.AsOfDate).IsRequired();
        builder.Property(s => s.TotalAssets).IsRequired();
        builder.Property(s => s.TotalLiabilities).IsRequired();

        // One snapshot per currency per date (CLAUDE.md: net worth is never blended across currencies) —
        // enforced here so a bug in the upsert logic can't silently create duplicate rows.
        builder.HasIndex(s => new { s.Currency, s.AsOfDate }).IsUnique();
    }
}
