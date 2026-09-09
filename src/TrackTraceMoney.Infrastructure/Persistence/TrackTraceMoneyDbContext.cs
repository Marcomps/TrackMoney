using Microsoft.EntityFrameworkCore;
using TrackTraceMoney.Domain.Accounts;
using TrackTraceMoney.Domain.Budgets;
using TrackTraceMoney.Domain.Categories;
using TrackTraceMoney.Domain.CreditAccounts;
using TrackTraceMoney.Domain.People;
using TrackTraceMoney.Domain.RecurringExpenses;
using TrackTraceMoney.Domain.Transactions;

namespace TrackTraceMoney.Infrastructure.Persistence;

public sealed class TrackTraceMoneyDbContext : DbContext
{
    public DbSet<Person> People => Set<Person>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<FinancialAccount> Accounts => Set<FinancialAccount>();

    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Budget> Budgets => Set<Budget>();

    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();

    public TrackTraceMoneyDbContext(DbContextOptions<TrackTraceMoneyDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackTraceMoneyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
