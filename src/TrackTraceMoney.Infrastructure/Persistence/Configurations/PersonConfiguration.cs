using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.People;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.RelationshipType).HasConversion<string>().IsRequired();
        builder.Property(p => p.CustomRelationshipLabel).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(2000);
    }
}
