using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Accounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table-per-hierarchy mapping for every asset account type. New asset account types (e.g. Phase 3
/// investment funds) get their own <c>.HasValue&lt;T&gt;("...")</c> line here plus their own
/// IEntityTypeConfiguration for type-specific columns — see BankAccountConfiguration/
/// SavingsAccountConfiguration/TermDepositConfiguration. Credit cards/loans (Phase 2) are liabilities
/// mapped by a separate TPH hierarchy rooted at <c>CreditAccount</c> (see CreditAccountConfiguration) —
/// they deliberately do not join this table/hierarchy.
/// </summary>
public sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Currency).HasConversion<string>().IsRequired();
        builder.Property(a => a.Balance).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasIndex(a => a.IsActive);

        builder.HasDiscriminator<string>("AccountType")
            .HasValue<CashAccount>("Cash")
            .HasValue<BankAccount>("Bank")
            .HasValue<SavingsAccount>("Savings")
            .HasValue<TermDeposit>("TermDeposit");
    }
}
