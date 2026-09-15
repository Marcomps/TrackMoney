using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Persistence.Configurations;

public sealed class CloudUserConfiguration : IEntityTypeConfiguration<CloudUser>
{
    public void Configure(EntityTypeBuilder<CloudUser> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();
    }
}
