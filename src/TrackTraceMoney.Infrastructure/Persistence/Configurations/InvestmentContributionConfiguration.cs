using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="InvestmentContribution.SourceAccountId"/> and
/// <see cref="InvestmentContribution.DestinationAccountId"/> intentionally SHARE the
/// <c>SourceAccountId</c>/<c>DestinationAccountId</c> columns with <see cref="Transfer"/> — same
/// semantic meaning (a paying-out and a receiving <see cref="Domain.Accounts.FinancialAccount"/>,
/// the receiving side here always being an <see cref="Domain.Accounts.InvestmentFund"/>). See this
/// repo's `.claude` memory (EF TPH shared-column gotcha).
/// </summary>
public sealed class InvestmentContributionConfiguration : IEntityTypeConfiguration<InvestmentContribution>
{
    public void Configure(EntityTypeBuilder<InvestmentContribution> builder)
    {
        builder.Property(c => c.SourceAccountId).IsRequired().HasColumnName("SourceAccountId");
        builder.Property(c => c.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");

        builder.HasIndex(c => c.SourceAccountId);
        builder.HasIndex(c => c.DestinationAccountId);
    }
}
