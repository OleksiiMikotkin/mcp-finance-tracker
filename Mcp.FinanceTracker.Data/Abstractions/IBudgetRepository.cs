namespace Mcp.FinanceTracker.Data.Abstractions;

public interface IBudgetRepository
{
    // monthlyLimit applies to the current calendar month and resets on the 1st — no custom periods.
    Task SetBudget(string category, decimal monthlyLimit);
}
