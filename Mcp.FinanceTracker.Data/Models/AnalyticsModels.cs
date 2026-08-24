namespace Mcp.FinanceTracker.Data.Models;

public sealed record CategoryTotal(string Category, decimal Total);

public sealed record MerchantTotal(string Merchant, decimal Total);

public sealed record CategorySpending(string Category, decimal Total, int Count, decimal Average);

public sealed record SubscriptionInfo(
    string Merchant, DateTime LastPaymentDate, decimal TypicalAmount,
    int PaymentCount, string Category, int? DaysInactive);

public sealed record SpendingPatternStats(
    string Category, decimal MonthlyAverage, int MonthsOfData,
    double MorningPct, double WeekdayPct,
    IReadOnlyList<MerchantTotal> TopMerchants, decimal TotalSpend, int TransactionCount);

public sealed record SpendingByHourStats(
    string Category, int AfterHour,
    int TotalCount, int AfterHourCount, double AfterHourPct,
    decimal TotalSpend, decimal AfterHourSpend, decimal AvgAfterHourAmount, decimal AvgOtherAmount);

public sealed record WeekendSpendingStats(
    decimal WeekdayAverage, decimal WeekendAverage, double WeekendSpikePct,
    decimal WeekdayTotal, decimal WeekendTotal, int WeekdayCount, int WeekendCount);

public sealed record SavingsSimulation(
    string Category, decimal ReductionPct, decimal MonthlyAverage,
    decimal MonthlySaving, decimal AnnualSaving, int MonthsOfData);

public sealed record MonthEndProjection(
    string Month, DateTime ReferenceDate,
    decimal Income, decimal ExpensesSoFar, decimal NetSoFar,
    decimal UpcomingRecurring, decimal UpcomingIncome, decimal ProjectedNet,
    IReadOnlyList<MerchantTotal> UpcomingItems);

public sealed record SuspiciousTransaction(long Id, DateTime Date, string Merchant, decimal Amount, string Category);

public sealed record ImpossibleTravelFlag(
    long FirstTransactionId, long SecondTransactionId,
    string FirstCountry, string SecondCountry,
    DateTime FirstDate, DateTime SecondDate, TimeSpan Gap);

public sealed record BudgetStatus(
    string Category, decimal MonthlyLimit, decimal SpentSoFar, decimal Remaining, double PercentUsed);
