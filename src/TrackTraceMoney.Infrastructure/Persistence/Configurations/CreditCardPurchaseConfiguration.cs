using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="CreditCardPurchase.CategoryId"/> MUST get an explicit <c>HasColumnName</c> — TPH
/// siblings with a same-named FK property (here, <see cref="Expense.CategoryId"/> and this type's
/// <see cref="CreditCardPurchase.CategoryId"/>) silently split into duplicate shadow columns without
/// it, which would make category-filtered queries stop finding card purchases. See this repo's
/// `.claude` memory (EF TPH shared-column gotcha) — the same fix already applied to
/// <c>ExpenseConfiguration</c>/<c>IncomeConfiguration</c>'s <c>CategoryId</c>.
/// <see cref="CreditCardPurchase.CreditAccountId"/> is intentionally named differently from
/// <see cref="Expense.AccountId"/> and must NOT share that column — one FKs into
/// <c>FinancialAccounts</c>, the other into <c>CreditAccounts</c>; sharing would corrupt data.
/// </summary>
public sealed class CreditCardPurchaseConfiguration : IEntityTypeConfiguration<CreditCardPurchase>
{
    public void Configure(EntityTypeBuilder<CreditCardPurchase> builder)
    {
        builder.Property(p => p.CreditAccountId).IsRequired().HasColumnName("CreditAccountId");
        builder.Property(p => p.CategoryId).IsRequired().HasColumnName("CategoryId");

        builder.HasIndex(p => p.CreditAccountId);
        builder.HasIndex(p => p.CategoryId);
    }
}
