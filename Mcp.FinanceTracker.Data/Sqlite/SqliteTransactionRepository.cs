using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Mcp.FinanceTracker.Data.Abstractions;
using Mcp.FinanceTracker.Data.Models;

namespace Mcp.FinanceTracker.Data.Sqlite;

internal sealed class SqliteTransactionRepository : ITransactionRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteTransactionRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<Transaction> AddTransaction(
        DateTime date, string merchant, decimal amount, string currency,
        string category, string account, bool recurring, string country)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Transactions (Date, Merchant, Amount, Currency, Category, Account, Recurring, Country, Status)
            VALUES ($date, $merchant, $amount, $currency, $category, $account, $recurring, $country, 'Posted')
            RETURNING Id;
            """;
        command.Parameters.AddWithValue("$date", SqliteTextUtils.FormatDate(date));
        command.Parameters.AddWithValue("$merchant", merchant);
        command.Parameters.AddWithValue("$amount", amount.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$currency", currency);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$account", account);
        command.Parameters.AddWithValue("$recurring", recurring ? 1 : 0);
        command.Parameters.AddWithValue("$country", country);

        var id = (long)(await command.ExecuteScalarAsync())!;
        return new Transaction(id, date, merchant, amount, currency, category, account, recurring, country, "Posted");
    }

    public async Task<bool> CategorizeTransaction(long id, string category)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Transactions SET Category = $category WHERE Id = $id;";
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$id", id);
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> ReverseTransaction(long id, string? reason)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Transactions SET Status = 'Reversed', ReversalReason = $reason
            WHERE Id = $id AND Status = 'Posted';
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsInPeriod(
        DateTime startDate, DateTime endDate,
        string? category = null, string? account = null, string? merchant = null, int? limit = null)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        var sql = new StringBuilder("""
            SELECT Id, Date, Merchant, Amount, Currency, Category, Account, Recurring, Country, Status
            FROM Transactions
            WHERE Date >= $startDate AND Date < $endDate
            """);
        command.Parameters.AddWithValue("$startDate", SqliteTextUtils.FormatDate(startDate));
        command.Parameters.AddWithValue("$endDate", SqliteTextUtils.FormatDate(endDate.AddDays(1)));

        if (category is not null)
        {
            sql.Append(" AND Category = $category");
            command.Parameters.AddWithValue("$category", category);
        }

        if (account is not null)
        {
            sql.Append(" AND Account = $account");
            command.Parameters.AddWithValue("$account", account);
        }

        if (merchant is not null)
        {
            sql.Append(" AND Merchant LIKE $merchant ESCAPE '\\'");
            command.Parameters.AddWithValue("$merchant", $"%{SqliteTextUtils.EscapeLike(merchant)}%");
        }

        sql.Append(" ORDER BY Date DESC");

        if (limit is not null)
        {
            sql.Append(" LIMIT $limit");
            command.Parameters.AddWithValue("$limit", limit.Value);
        }

        command.CommandText = sql.ToString();

        var results = new List<Transaction>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadTransaction(reader));
        }

        return results;
    }

    private static Transaction ReadTransaction(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
        reader.GetString(2),
        decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetInt64(7) == 1,
        reader.GetString(8),
        reader.GetString(9));
}
