using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Budgets;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Amount).IsRequired();
        builder.Property(b => b.Year).IsRequired();
        builder.Property(b => b.Month).IsRequired();

        // One budget per category per month (README §34).
        builder.HasIndex(b => new { b.CategoryId, b.Year, b.Month }).IsUnique();
    }
}
