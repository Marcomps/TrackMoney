using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.CardNetworks;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.Institutions;
using TrackTraceMoney.Domain.MedicalExpenses;
using TrackTraceMoney.Domain.NetWorth;
using TrackTraceMoney.Domain.People;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence;

public sealed class TrackTraceMoneyDbContext : DbContext
{
    public DbSet<Person> People => Set<Person>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<FinancialInstitution> FinancialInstitutions => Set<FinancialInstitution>();

    public DbSet<CardNetwork> CardNetworks => Set<CardNetwork>();

    public DbSet<FinancialAccount> Accounts => Set<FinancialAccount>();

    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Budget> Budgets => Set<Budget>();

    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();

    public DbSet<CreditCardStatement> CreditCardStatements => Set<CreditCardStatement>();

    public DbSet<InvestmentValuation> InvestmentValuations => Set<InvestmentValuation>();

    public DbSet<NetWorthSnapshot> NetWorthSnapshots => Set<NetWorthSnapshot>();

    public DbSet<MedicalExpenseDetail> MedicalExpenseDetails => Set<MedicalExpenseDetail>();

    public TrackTraceMoneyDbContext(DbContextOptions<TrackTraceMoneyDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Excludes TrackTraceMoney.Infrastructure.Profiles: those IEntityTypeConfiguration<T>
        // classes (LocalProfileGroupConfiguration/LocalProfileConfiguration) belong exclusively to
        // ProfileCatalogDbContext's own, separately-migrated database. ApplyConfigurationsFromAssembly
        // scans the whole assembly regardless of which DbContext a config was written for, so without
        // this filter it silently pulls LocalProfileGroup/LocalProfile into this context's model too --
        // which this context's own migration history knows nothing about, tripping EF Core's
        // PendingModelChangesWarning (thrown as a fatal error by default) the moment Database.Migrate()
        // runs, on every single app launch.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(TrackTraceMoneyDbContext).Assembly,
            t => t.Namespace is null || !t.Namespace.StartsWith("TrackTraceMoney.Infrastructure.Profiles", StringComparison.Ordinal));
        base.OnModelCreating(modelBuilder);
    }
}
