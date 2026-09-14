using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.MedicalExpenses;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class MedicalExpenseDetailConfiguration : IEntityTypeConfiguration<MedicalExpenseDetail>
{
    public void Configure(EntityTypeBuilder<MedicalExpenseDetail> builder)
    {
        builder.ToTable("MedicalExpenseDetails");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TransactionId).IsRequired();

        builder.HasIndex(d => d.TransactionId).IsUnique();
    }
}
