using Mcp.FinanceTracker.Data.Models;

namespace Mcp.FinanceTracker.Data.Abstractions;

public interface IFinanceAnalyticsQueries
{
    Task<decimal> GetBalance();

    Task<BudgetStatus> GetBudgetStatus(string category);

    Task<IReadOnlyList<CategoryTotal>> GetTopCategories(DateTime startDate, DateTime endDate, int limit = 5);

    Task<CategorySpending> GetSpendingByCategory(DateTime startDate, DateTime endDate, string category);

    Task<IReadOnlyList<MerchantTotal>> GetSpendingByMerchant(DateTime startDate, DateTime endDate, int limit = 10);

    Task<IReadOnlyList<SubscriptionInfo>> DetectSubscriptions(bool onlyForgotten = false, int minInactiveDays = 60);

    Task<SpendingPatternStats> AnalyzeSpendingPattern(string category);

    Task<SpendingByHourStats> AnalyzeSpendingByHour(string category, int afterHour);

    Task<WeekendSpendingStats> AnalyzeWeekendSpending();

    Task<SavingsSimulation> SimulateSavings(string category, decimal reductionPct, string? merchant = null);

    Task<MonthEndProjection> ProjectMonthEndBalance();

    Task<IReadOnlyList<SuspiciousTransaction>> DetectSuspiciousTransactions();

    Task<IReadOnlyList<ImpossibleTravelFlag>> DetectImpossibleTravel();
}
