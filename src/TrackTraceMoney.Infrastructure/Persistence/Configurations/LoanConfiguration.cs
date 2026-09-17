using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        // Legacy free-text field, now nullable (superseded by InstitutionId) — see Loan.Institution's
        // own remarks. No longer .IsRequired(): the C# property itself is nullable now. Doesn't collide
        // with anything on this table (CreditCard has no "Institution" property, only "Issuer"), so no
        // HasColumnName pin needed for this one.
        builder.Property(l => l.Institution).HasMaxLength(200);

        // InstitutionId also exists on the sibling CreditCard subtype in this same CreditAccount TPH
        // hierarchy (a new collision, introduced by this slice) — pin the column name explicitly here
        // (and on CreditCardConfiguration) or EF splits them into two physical columns (this repo's
        // documented ef-tph-shared-column-gotcha).
        builder.Property(l => l.InstitutionId).HasColumnName("InstitutionId");

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
