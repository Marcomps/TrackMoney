using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="InvestmentWithdrawal.SourceAccountId"/> and
/// <see cref="InvestmentWithdrawal.DestinationAccountId"/> intentionally SHARE the
/// <c>SourceAccountId</c>/<c>DestinationAccountId</c> columns with <see cref="Transfer"/> — same
/// semantic meaning (a paying-out and a receiving <see cref="Domain.Accounts.FinancialAccount"/>,
/// the paying-out side here always being an <see cref="Domain.Accounts.InvestmentFund"/>). See this
/// repo's `.claude` memory (EF TPH shared-column gotcha).
/// </summary>
public sealed class InvestmentWithdrawalConfiguration : IEntityTypeConfiguration<InvestmentWithdrawal>
{
    public void Configure(EntityTypeBuilder<InvestmentWithdrawal> builder)
    {
        builder.Property(w => w.SourceAccountId).IsRequired().HasColumnName("SourceAccountId");
        builder.Property(w => w.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");

        builder.HasIndex(w => w.SourceAccountId);
        builder.HasIndex(w => w.DestinationAccountId);
    }
}
