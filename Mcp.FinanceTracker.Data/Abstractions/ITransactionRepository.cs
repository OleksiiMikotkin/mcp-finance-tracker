using Mcp.FinanceTracker.Data.Models;

namespace Mcp.FinanceTracker.Data.Abstractions;

public interface ITransactionRepository
{
    Task<Transaction> AddTransaction(
        DateTime date, string merchant, decimal amount, string currency,
        string category, string account, bool recurring, string country);

    Task<bool> CategorizeTransaction(long id, string category);

    Task<bool> ReverseTransaction(long id, string? reason);

    Task<IReadOnlyList<Transaction>> GetTransactionsInPeriod(
        DateTime startDate, DateTime endDate,
        string? category = null, string? account = null, string? merchant = null, int? limit = null);
}
