using Microsoft.Extensions.DependencyInjection;
using Mcp.FinanceTracker.Data.Abstractions;
using Mcp.FinanceTracker.Data.Sqlite;

namespace Mcp.FinanceTracker.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinanceData(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton(new SqliteConnectionFactory(databasePath));
        services.AddSingleton<FinanceDbInitializer>();
        services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
        services.AddSingleton<IBudgetRepository, SqliteBudgetRepository>();
        services.AddSingleton<IFinanceAnalyticsQueries, SqliteFinanceAnalyticsQueries>();
        return services;
    }

    public static async Task EnsureFinanceDataSeededAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var initializer = services.GetRequiredService<FinanceDbInitializer>();
        await initializer.EnsureCreatedAndSeededAsync(cancellationToken);
    }
}
