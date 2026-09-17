using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Institutions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class FinancialInstitutionConfiguration : IEntityTypeConfiguration<FinancialInstitution>
{
    public void Configure(EntityTypeBuilder<FinancialInstitution> builder)
    {
        builder.ToTable("FinancialInstitutions");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
    }
}
