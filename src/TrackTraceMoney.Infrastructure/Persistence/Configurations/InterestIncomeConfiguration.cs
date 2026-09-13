using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="InterestIncome.DestinationAccountId"/> intentionally SHARES the
/// <c>DestinationAccountId</c> column with <see cref="Income"/>/<see cref="Transfer"/>/
/// <see cref="InvestmentContribution"/>/<see cref="InvestmentWithdrawal"/> — same semantic meaning
/// (a receiving <see cref="Domain.Accounts.FinancialAccount"/>, here always a
/// <see cref="Domain.Accounts.TermDeposit"/>). See this repo's `.claude` memory (EF TPH shared-column
/// gotcha).
/// </summary>
public sealed class InterestIncomeConfiguration : IEntityTypeConfiguration<InterestIncome>
{
    public void Configure(EntityTypeBuilder<InterestIncome> builder)
    {
        builder.Property(i => i.DestinationAccountId).IsRequired().HasColumnName("DestinationAccountId");
        builder.HasIndex(i => i.DestinationAccountId);
    }
}
