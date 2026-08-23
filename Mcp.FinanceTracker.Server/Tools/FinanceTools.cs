using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Mcp.FinanceTracker.Server.Tools;

[McpServerToolType]
public static class FinanceTools
{
    private static readonly List<Transaction> Transactions = [];

    [McpServerTool, Description("Add a transaction to the ledger. Use a negative amount for expenses and a positive amount for income.")]
    public static string AddTransaction(
        [Description("Transaction amount. Negative for expenses, positive for income.")] decimal amount,
        [Description("Short description of the transaction, e.g. merchant or source name.")] string description)
    {
        Transactions.Add(new Transaction(amount, description, DateTime.UtcNow));
        return $"Transaction added: {amount:0.00} — {description}";
    }

    [McpServerTool, Description("Return the current balance, calculated as the sum of all recorded transactions.")]
    public static decimal GetBalance()
    {
        return Transactions.Sum(t => t.Amount);
    }

    private sealed record Transaction(decimal Amount, string Description, DateTime Timestamp);
}
