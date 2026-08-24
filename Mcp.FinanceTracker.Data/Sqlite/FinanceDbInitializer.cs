using System.Globalization;
using Microsoft.Data.Sqlite;
using Mcp.FinanceTracker.Data.Common;

namespace Mcp.FinanceTracker.Data.Sqlite;

internal sealed class FinanceDbInitializer
{
    private static readonly string CreateSchemaSql = $"""
        CREATE TABLE IF NOT EXISTS Transactions (
            Id        INTEGER PRIMARY KEY AUTOINCREMENT,
            Date      TEXT    NOT NULL,
            Merchant  TEXT    NOT NULL,
            Amount    TEXT    NOT NULL,
            Currency  TEXT    NOT NULL,
            Category  TEXT    NOT NULL CHECK (Category IN ({FinanceCategories.AsSqlList()})),
            Account   TEXT    NOT NULL CHECK (Account IN ('main_debit','credit_card')),
            Recurring INTEGER NOT NULL CHECK (Recurring IN (0, 1)),
            Country   TEXT    NOT NULL,
            Status    TEXT    NOT NULL CHECK (Status IN ('Posted', 'Reversed')),
            ReversalReason TEXT,
            CreatedAt TEXT    NOT NULL DEFAULT (datetime('now'))
        );
        CREATE INDEX IF NOT EXISTS IX_Transactions_Date ON Transactions(Date);
        CREATE INDEX IF NOT EXISTS IX_Transactions_Category ON Transactions(Category);

        CREATE TABLE IF NOT EXISTS Budgets (
            Category     TEXT PRIMARY KEY CHECK (Category IN ({FinanceCategories.AsSqlList()})),
            MonthlyLimit TEXT NOT NULL,
            UpdatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    private readonly SqliteConnectionFactory connectionFactory;
    private readonly string seedCsvPath;

    public FinanceDbInitializer(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
        this.seedCsvPath = Path.Combine(AppContext.BaseDirectory, "Seed", "transactions.csv");
    }

    public async Task EnsureCreatedAndSeededAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync(cancellationToken);

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = CreateSchemaSql;
            await createCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Transactions;";
        var existingCount = (long)(await countCommand.ExecuteScalarAsync(cancellationToken))!;
        if (existingCount > 0)
        {
            return;
        }

        await SeedFromCsvAsync(connection, cancellationToken);
    }

    private async Task SeedFromCsvAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!File.Exists(this.seedCsvPath))
        {
            return;
        }

        await using var transaction = connection.BeginTransaction();
        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = """
            INSERT INTO Transactions (Date, Merchant, Amount, Currency, Category, Account, Recurring, Country, Status)
            VALUES ($date, $merchant, $amount, $currency, $category, $account, $recurring, $country, $status);
            """;

        var dateParam = insertCommand.Parameters.Add("$date", SqliteType.Text);
        var merchantParam = insertCommand.Parameters.Add("$merchant", SqliteType.Text);
        var amountParam = insertCommand.Parameters.Add("$amount", SqliteType.Text);
        var currencyParam = insertCommand.Parameters.Add("$currency", SqliteType.Text);
        var categoryParam = insertCommand.Parameters.Add("$category", SqliteType.Text);
        var accountParam = insertCommand.Parameters.Add("$account", SqliteType.Text);
        var recurringParam = insertCommand.Parameters.Add("$recurring", SqliteType.Integer);
        var countryParam = insertCommand.Parameters.Add("$country", SqliteType.Text);
        var statusParam = insertCommand.Parameters.Add("$status", SqliteType.Text);

        using var reader = new StreamReader(this.seedCsvPath);
        await reader.ReadLineAsync(cancellationToken); // skip header

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split(',');
            dateParam.Value = fields[0];
            merchantParam.Value = fields[1];
            amountParam.Value = decimal.Parse(fields[2], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            currencyParam.Value = fields[3];
            categoryParam.Value = fields[4];
            accountParam.Value = fields[5];
            recurringParam.Value = string.Equals(fields[6], "True", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            countryParam.Value = fields[7];
            statusParam.Value = fields[8];

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
