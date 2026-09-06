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

        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IFinancialAccountRepository, FinancialAccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();

        return services;
    }
}
