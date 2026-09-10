using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.Property(l => l.Institution).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Kind).HasConversion<string>().IsRequired();
        builder.Property(l => l.OriginalAmount).IsRequired();
        builder.Property(l => l.InterestRate).IsRequired();
        builder.Property(l => l.RateType).HasConversion<string>().IsRequired();
        builder.Property(l => l.MonthlyInstallment).IsRequired();
        builder.Property(l => l.NextPaymentDate).IsRequired();
        builder.Property(l => l.RequiredPayment).IsRequired();
        builder.Property(l => l.Fees);
        // RemainingPayments is computed, not mapped — mirrors CreditCard.AvailableCredit.
    }
}
