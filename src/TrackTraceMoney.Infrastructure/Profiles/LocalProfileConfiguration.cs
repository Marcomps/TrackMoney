using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Profiles;

namespace TrackTraceMoney.Infrastructure.Profiles;

public sealed class LocalProfileConfiguration : IEntityTypeConfiguration<LocalProfile>
{
    public void Configure(EntityTypeBuilder<LocalProfile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.LocalProfileGroupId).IsRequired();

        // Non-unique lookup index -- mirrors the plain-scalar-FK-plus-index convention used
        // throughout Persistence/Configurations (e.g. ExpenseConfiguration's AccountId/CategoryId),
        // not a Fluent API HasOne/WithMany relationship, since LocalProfile has no navigation
        // property back to LocalProfileGroup (same "no nav property" shape as those examples).
        builder.HasIndex(p => p.LocalProfileGroupId);
    }
}
