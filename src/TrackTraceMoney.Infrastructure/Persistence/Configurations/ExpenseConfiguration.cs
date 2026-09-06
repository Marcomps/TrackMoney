using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.Property(e => e.AccountId).IsRequired();
        builder.Property(e => e.CategoryId).IsRequired().HasColumnName("CategoryId");

        builder.HasIndex(e => e.AccountId);
        builder.HasIndex(e => e.CategoryId);
    }
}
