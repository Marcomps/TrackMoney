using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table-per-hierarchy mapping for every transaction kind. Phase 2/3 kinds
/// (CreditCardPurchase/Payment, LoanPayment, InvestmentContribution/Withdrawal, Reimbursement, ...)
/// each get their own <c>.HasValue&lt;T&gt;("...")</c> line here when they're built.
/// </summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Amount).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Notes).HasMaxLength(2000);

        builder.HasIndex(t => t.Date);

        builder.HasDiscriminator<string>("TransactionType")
            .HasValue<Income>("Income")
            .HasValue<Expense>("Expense")
            .HasValue<Transfer>("Transfer")
            .HasValue<CreditCardPurchase>("CreditCardPurchase");
    }
}
