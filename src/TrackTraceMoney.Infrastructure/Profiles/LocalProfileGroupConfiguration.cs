using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Profiles;

namespace TrackTraceMoney.Infrastructure.Profiles;

public sealed class LocalProfileGroupConfiguration : IEntityTypeConfiguration<LocalProfileGroup>
{
    public void Configure(EntityTypeBuilder<LocalProfileGroup> builder)
    {
        builder.ToTable("ProfileGroups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
    }
}
