using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.Property(c => c.Issuer).IsRequired().HasMaxLength(200);
        builder.Property(c => c.LastFourDigits).HasMaxLength(4);
        builder.Property(c => c.CreditLimit).IsRequired();
        builder.Property(c => c.AnnualInterestRate);
        builder.Property(c => c.MonthlyInterestRate);
        builder.Property(c => c.StatementCutOffDay).IsRequired();
        builder.Property(c => c.PaymentDueDay).IsRequired();
    }
}
