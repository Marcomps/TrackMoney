using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrackTraceMoney.Application.Abstractions;
using TrackTraceMoney.Infrastructure.Persistence;
using TrackTraceMoney.Infrastructure.Repositories;

namespace TrackTraceMoney.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTrackTraceMoneyInfrastructure(this IServiceCollection services, string sqliteConnectionString)
    {
        services.AddDbContext<TrackTraceMoneyDbContext>(options => options.UseSqlite(sqliteConnectionString));

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
        services.AddScoped<ILocalBackupService, LocalBackupService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
