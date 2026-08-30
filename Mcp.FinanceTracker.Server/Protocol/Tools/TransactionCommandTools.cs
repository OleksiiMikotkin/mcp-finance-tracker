using System.ComponentModel;
using System.Globalization;
using Mcp.FinanceTracker.Data.Abstractions;
using ModelContextProtocol.Server;

namespace Mcp.FinanceTracker.Server.Protocol.Tools;

[McpServerToolType]
public sealed class TransactionCommandTools
{
    private readonly ITransactionRepository transactions;
    private readonly IBudgetRepository budgets;

    public TransactionCommandTools(ITransactionRepository transactions, IBudgetRepository budgets)
    {
        this.transactions = transactions;
        this.budgets = budgets;
    }

    [McpServerTool(Name = "add_transaction")]
    [Description("Add a new transaction to the ledger. Use a negative amount for expenses and a positive amount for income.")]
    public async Task<string> AddTransaction(
        [Description("Transaction date and time, ISO 8601 (e.g. '2026-08-24T09:00'). Use the current date/time if the user doesn't specify one.")] DateTime date,
        [Description("Merchant or income source name, e.g. 'Lidl' or 'Muster GmbH'.")] string merchant,
        [Description("Transaction amount. Negative for expenses, positive for income.")] decimal amount,
        [Description("ISO 4217 currency code, e.g. 'EUR'.")] string currency,
        [Description("Spending category, e.g. 'coffee', 'groceries', 'subscriptions', 'salary'.")] string category,
        [Description("Account the transaction was made on: 'main_debit' or 'credit_card'.")] string account,
        [Description("Whether this is a recurring charge (subscription, utility bill, salary, etc.).")] bool recurring,
        [Description("ISO 3166-1 alpha-2 country code where the transaction took place, e.g. 'DE'.")] string country)
    {
        var added = await this.transactions.AddTransaction(date, merchant, amount, currency, category, account, recurring, country);
        return $"Transaction #{added.Id} added: {added.Amount.ToString(CultureInfo.InvariantCulture)} {added.Currency} — {added.Merchant} ({added.Category}).";
    }

    [McpServerTool(Name = "categorize_transaction")]
    [Description("Change the category of an existing transaction.")]
    public async Task<string> CategorizeTransaction(
        [Description("Id of the transaction to update.")] long id,
        [Description("New spending category, e.g. 'coffee', 'groceries', 'entertainment'.")] string category)
    {
        var updated = await this.transactions.CategorizeTransaction(id, category);
        return updated
            ? $"Transaction #{id} recategorized to '{category}'."
            : $"Transaction #{id} not found.";
    }

    [McpServerTool(Name = "reverse_transaction")]
    [Description("Reverse (void) a transaction instead of deleting it, preserving an audit trail. Use this for corrections, refunds, or disputed charges.")]
    public async Task<string> ReverseTransaction(
        [Description("Id of the transaction to reverse.")] long id,
        [Description("Optional reason for the reversal, e.g. 'duplicate charge' or 'refunded by merchant'.")] string? reason = null)
    {
        var reversed = await this.transactions.ReverseTransaction(id, reason);
        return reversed
            ? $"Transaction #{id} reversed."
            : $"Transaction #{id} not found or already reversed.";
    }

    [McpServerTool(Name = "set_budget")]
    [Description("Set (or update) the monthly spending limit for a category. The limit applies to the current calendar month and resets on the 1st.")]
    public async Task<string> SetBudget(
        [Description("Spending category, e.g. 'coffee', 'delivery', 'entertainment'.")] string category,
        [Description("Monthly spending limit for this category.")] decimal monthlyLimit)
    {
        await this.budgets.SetBudget(category, monthlyLimit);
        return $"Budget for '{category}' set to {monthlyLimit.ToString(CultureInfo.InvariantCulture)}/month.";
    }
}
