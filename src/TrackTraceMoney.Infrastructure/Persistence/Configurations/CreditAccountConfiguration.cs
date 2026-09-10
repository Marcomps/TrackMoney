using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.CreditAccounts;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table-per-hierarchy mapping for every liability account type — a separate hierarchy from
/// <see cref="TrackTraceMoney.Domain.Accounts.FinancialAccount"/>'s (see FinancialAccountConfiguration's
/// remarks). New liability account types get their own <c>.HasValue&lt;T&gt;("...")</c> line here plus
/// their own IEntityTypeConfiguration for type-specific columns — see CreditCardConfiguration.
/// </summary>
public sealed class CreditAccountConfiguration : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.ToTable("CreditAccounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Currency).HasConversion<string>().IsRequired();
        builder.Property(a => a.AmountOwed).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasIndex(a => a.IsActive);

        builder.HasDiscriminator<string>("CreditAccountType")
            .HasValue<CreditCard>("CreditCard")
            .HasValue<Loan>("Loan");
    }
}
