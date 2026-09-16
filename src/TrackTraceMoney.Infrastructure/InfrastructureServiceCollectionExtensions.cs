using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Infrastructure.Persistence;
using TrackTraceMoney.Infrastructure.Repositories;

namespace TrackTraceMoney.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTrackTraceMoneyInfrastructure(this IServiceCollection services, Func<IServiceProvider, string> sqliteConnectionStringFactory)
    {
        // Resolved lazily, at first-actual-use time (i.e. when AddDbContext's options delegate first
        // runs), not eagerly at DI-registration time -- callers that don't yet know the final
        // connection string when they register services (e.g. a profile-aware path that depends on
        // which local profile is active) can still register here and supply that value later.
        services.AddDbContext<TrackTraceMoneyDbContext>((sp, options) => options.UseSqlite(sqliteConnectionStringFactory(sp)));

        // Singleton is deliberate, not the default Scoped: this app's DI shape (see
        // DbAccessGate's remarks) already resolves the "Scoped" TrackTraceMoneyDbContext as a single,
        // long-lived instance for the whole app session, so the gate protecting concurrent access to
        // it must be a single instance too — matching that reality explicitly here (rather than
        // registering Scoped and relying on the same root-container quirk) makes the intent obvious
        // and keeps it correct even if a future change introduces real per-navigation scopes.
        services.AddSingleton<IDbAccessGate, DbAccessGate>();

        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IFinancialAccountRepository, FinancialAccountRepository>();
        services.AddScoped<ICreditAccountRepository, CreditAccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IRecurringExpenseRepository, RecurringExpenseRepository>();
        services.AddScoped<ICreditCardStatementRepository, CreditCardStatementRepository>();
        services.AddScoped<IInvestmentValuationRepository, InvestmentValuationRepository>();
        services.AddScoped<INetWorthSnapshotRepository, NetWorthSnapshotRepository>();
        services.AddScoped<IMedicalExpenseDetailRepository, MedicalExpenseDetailRepository>();
        services.AddScoped<ILocalBackupService, LocalBackupService>();
        services.AddScoped<IFinanceDatabaseInitializer, FinanceDatabaseInitializer>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
