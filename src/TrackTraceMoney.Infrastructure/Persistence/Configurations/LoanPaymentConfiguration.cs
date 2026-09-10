using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="LoanPayment.SourceAccountId"/> intentionally SHARES the <c>SourceAccountId</c> column
/// with <see cref="Transfer.SourceAccountId"/>/<see cref="CreditCardPayment.SourceAccountId"/> — same
/// semantic meaning (the paying-out <see cref="Domain.Accounts.FinancialAccount"/>).
/// <see cref="LoanPayment.CreditAccountId"/> intentionally SHARES the <c>CreditAccountId</c> column
/// with <see cref="CreditCardPurchase.CreditAccountId"/>/<see cref="CreditCardPayment.CreditAccountId"/>
/// — same semantic meaning (the target <see cref="Domain.CreditAccounts.CreditAccount"/>). See this
/// repo's `.claude` memory (EF TPH shared-column gotcha).
/// </summary>
public sealed class LoanPaymentConfiguration : IEntityTypeConfiguration<LoanPayment>
{
    public void Configure(EntityTypeBuilder<LoanPayment> builder)
    {
        builder.Property(p => p.SourceAccountId).IsRequired().HasColumnName("SourceAccountId");
        builder.Property(p => p.CreditAccountId).IsRequired().HasColumnName("CreditAccountId");

        builder.HasIndex(p => p.SourceAccountId);
        builder.HasIndex(p => p.CreditAccountId);
    }
}
