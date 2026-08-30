using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Mcp.FinanceTracker.Data.Abstractions;
using Mcp.FinanceTracker.Data.Common;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mcp.FinanceTracker.Server.Protocol.Resources;

[McpServerResourceType]
public sealed class FinanceResources
{
    private readonly ITransactionRepository transactions;
    private readonly IFinanceAnalyticsQueries analytics;

    public FinanceResources(ITransactionRepository transactions, IFinanceAnalyticsQueries analytics)
    {
        this.transactions = transactions;
        this.analytics = analytics;
    }

    [McpServerResource(UriTemplate = "finance://accounts", Name = "accounts", MimeType = "application/json")]
    [Description("List of available accounts with their id and current balance. Use an entry's id with finance://account/{id} to read that account individually.")]
    public async Task<TextResourceContents> GetAccounts()
    {
        var results = new List<object>();
        foreach (var account in AccountIdentifiers.All)
        {
            var balance = await this.analytics.GetAccountBalance(account);
            results.Add(new
            {
                id = AccountIdentifiers.ToUriId(account),
                account,
                balance,
            });
        }

        return new TextResourceContents
        {
            Uri = "finance://accounts",
            MimeType = "application/json",
            Text = JsonSerializer.Serialize(results),
        };
    }

    [McpServerResource(UriTemplate = "finance://account/{id}", Name = "account_balance", MimeType = "application/json")]
    [Description("Balance of a single account. Id is the IBAN for the main debit account, or 'card-4242' for the credit card.")]
    public async Task<TextResourceContents> GetAccountBalance(
        [Description("Account id: IBAN for the debit account, or 'card-4242' for the credit card.")] string id)
    {
        id = id.Trim();
        var account = AccountIdentifiers.ToAccountCode(id)
            ?? throw new McpException($"Unknown account id '{id}'.");
        var balance = await this.analytics.GetAccountBalance(account);

        var json = JsonSerializer.Serialize(new
        {
            id,
            account,
            balance,
        });

        return new TextResourceContents
        {
            Uri = $"finance://account/{id}",
            MimeType = "application/json",
            Text = json,
        };
    }

    [McpServerResource(UriTemplate = "finance://transactions/{period}", Name = "transactions_by_period", MimeType = "application/json")]
    [Description("All transactions for a calendar month, identified by period in YYYY-MM format.")]
    public async Task<TextResourceContents> GetTransactionsByPeriod(
        [Description("Calendar month, format YYYY-MM, e.g. '2026-08'.")] string period)
    {
        period = period.Trim();
        if (!DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
        {
            throw new McpException($"Invalid period '{period}'. Expected format YYYY-MM.");
        }

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var results = await this.transactions.GetTransactionsInPeriod(monthStart, monthEnd);

        var json = JsonSerializer.Serialize(results);

        return new TextResourceContents
        {
            Uri = $"finance://transactions/{period}",
            MimeType = "application/json",
            Text = json,
        };
    }
}
