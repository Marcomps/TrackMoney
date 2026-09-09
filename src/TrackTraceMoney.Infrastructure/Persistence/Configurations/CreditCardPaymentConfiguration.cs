using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="CreditCardPayment.SourceAccountId"/> intentionally SHARES the <c>SourceAccountId</c>
/// column with <see cref="Transfer.SourceAccountId"/> — same semantic meaning (the paying-out
/// <see cref="Domain.Accounts.FinancialAccount"/>). <see cref="CreditCardPayment.CreditAccountId"/>
/// intentionally SHARES the <c>CreditAccountId</c> column with
/// <see cref="CreditCardPurchase.CreditAccountId"/> — same semantic meaning (the target
/// <see cref="Domain.CreditAccounts.CreditAccount"/>). See this repo's `.claude` memory (EF TPH
/// shared-column gotcha).
/// </summary>
public sealed class CreditCardPaymentConfiguration : IEntityTypeConfiguration<CreditCardPayment>
{
    public void Configure(EntityTypeBuilder<CreditCardPayment> builder)
    {
        builder.Property(p => p.SourceAccountId).IsRequired().HasColumnName("SourceAccountId");
        builder.Property(p => p.CreditAccountId).IsRequired().HasColumnName("CreditAccountId");

        builder.HasIndex(p => p.SourceAccountId);
        builder.HasIndex(p => p.CreditAccountId);
    }
}
