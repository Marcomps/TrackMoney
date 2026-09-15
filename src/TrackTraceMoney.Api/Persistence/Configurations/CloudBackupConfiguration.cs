using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Api.Backups;
using TrackTraceMoney.Api.Users;

namespace TrackTraceMoney.Api.Persistence.Configurations;

public sealed class CloudBackupConfiguration : IEntityTypeConfiguration<CloudBackup>
{
    public void Configure(EntityTypeBuilder<CloudBackup> builder)
    {
        builder.ToTable("CloudBackups");

        builder.HasKey(b => b.Id);

        builder.HasIndex(b => b.UserId)
            .IsUnique();

        builder.Property(b => b.Data)
            .IsRequired()
            .HasColumnType("bytea");

        builder.HasOne<CloudUser>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
