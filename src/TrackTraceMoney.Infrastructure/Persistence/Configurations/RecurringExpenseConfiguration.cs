using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.RecurringExpenses;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("RecurringExpenses");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Amount).IsRequired();
        builder.Property(r => r.CategoryId).IsRequired();
        builder.Property(r => r.AccountId).IsRequired();
        builder.Property(r => r.Frequency).HasConversion<string>().IsRequired();
        builder.Property(r => r.StartDate).IsRequired();
        builder.Property(r => r.EndDate);
        builder.Property(r => r.LastConfirmedDate);
        builder.Property(r => r.IsActive).IsRequired();

        builder.HasIndex(r => r.IsActive);
    }
}
