using System.Globalization;
using Mcp.FinanceTracker.Data.Abstractions;

namespace Mcp.FinanceTracker.Data.Sqlite;

internal sealed class SqliteBudgetRepository : IBudgetRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteBudgetRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task SetBudget(string category, decimal monthlyLimit)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Budgets (Category, MonthlyLimit, UpdatedAt)
            VALUES ($category, $monthlyLimit, datetime('now'))
            ON CONFLICT(Category) DO UPDATE SET
                MonthlyLimit = excluded.MonthlyLimit,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$monthlyLimit", monthlyLimit.ToString(CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }
}
