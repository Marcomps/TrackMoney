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
        builder.Property(b => b.Currency).HasConversion<string>().IsRequired();

        // One budget per category per month per currency (README §34) — a category can have separate
        // budgets in different currencies if it's spent from accounts in more than one currency.
        builder.HasIndex(b => new { b.CategoryId, b.Year, b.Month, b.Currency }).IsUnique();
    }
}
