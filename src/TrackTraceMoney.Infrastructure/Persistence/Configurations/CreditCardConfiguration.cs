using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        // Legacy free-text field, now nullable (superseded by InstitutionId) — see CreditCard.Issuer's
        // own remarks. No longer .IsRequired(): the C# property itself is nullable now.
        builder.Property(c => c.Issuer).HasMaxLength(200);

        // InstitutionId also exists on the sibling Loan subtype in this same CreditAccount TPH
        // hierarchy (a new collision, introduced by this slice — the two used to be named Issuer/
        // Institution and never collided) — pin the column name explicitly here (and on
        // LoanConfiguration) or EF splits them into two physical columns (this repo's documented
        // ef-tph-shared-column-gotcha).
        builder.Property(c => c.InstitutionId).HasColumnName("InstitutionId");

        // NetworkId is CreditCard-only (Slice B) — no sibling collision, no pin needed.
        builder.Property(c => c.NetworkId);

        builder.Property(c => c.LastFourDigits).HasMaxLength(4);
        builder.Property(c => c.CreditLimit).IsRequired();
        builder.Property(c => c.AnnualInterestRate);
        builder.Property(c => c.MonthlyInterestRate);
        builder.Property(c => c.StatementCutOffDay).IsRequired();
        builder.Property(c => c.PaymentDueDay).IsRequired();
    }
}
