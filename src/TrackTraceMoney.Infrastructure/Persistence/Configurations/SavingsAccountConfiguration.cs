using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class SavingsAccountConfiguration : IEntityTypeConfiguration<SavingsAccount>
{
    public void Configure(EntityTypeBuilder<SavingsAccount> builder)
    {
        builder.Property(a => a.GoalAmount);
    }
}
