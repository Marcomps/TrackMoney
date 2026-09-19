using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        // Legacy free-text field, now nullable (superseded by InstitutionId) — see BankAccount.BankName's
        // own remarks. No collision risk: no sibling FinancialAccount subtype has a "BankName" property.
        builder.Property(a => a.BankName).HasMaxLength(200);

        // InstitutionId collides with the identically-named property on the sibling TermDeposit/
        // InvestmentFund subtypes in this same FinancialAccount TPH hierarchy (this repo's documented
        // ef-tph-shared-column-gotcha) — pin the column name explicitly here too, matching
        // TermDepositConfiguration/InvestmentFundConfiguration's existing pin, so all three land on the
        // same physical "InstitutionId" column instead of three separate ones.
        builder.Property(a => a.InstitutionId).HasColumnName("InstitutionId");

        builder.Property(a => a.AccountNumberLast4).HasMaxLength(4);
    }
}
