using System.Globalization;
using Microsoft.Data.Sqlite;
using Mcp.FinanceTracker.Data.Abstractions;
using Mcp.FinanceTracker.Data.Models;

namespace Mcp.FinanceTracker.Data.Sqlite;

internal sealed class SqliteFinanceAnalyticsQueries : IFinanceAnalyticsQueries
{
    private static readonly HashSet<string> IncomeCategories = new(StringComparer.Ordinal)
    {
        "salary", "credit_payment",
    };

    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteFinanceAnalyticsQueries(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<decimal> GetBalance()
    {
        var rows = await this.QueryRowsAsync("");
        return rows.Sum(r => r.Amount);
    }

    public async Task<BudgetStatus> GetBudgetStatus(string category)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var budgetCommand = connection.CreateCommand();
        budgetCommand.CommandText = "SELECT MonthlyLimit FROM Budgets WHERE Category = $category;";
        budgetCommand.Parameters.AddWithValue("$category", category);
        var limitText = (string?)await budgetCommand.ExecuteScalarAsync();
        if (limitText is null)
        {
            throw new InvalidOperationException($"No budget has been set for category '{category}'. Call SetBudget first.");
        }

        var monthlyLimit = decimal.Parse(limitText, CultureInfo.InvariantCulture);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var spentRows = await this.QueryRowsAsync(
            "AND Category = $category AND Amount < 0 AND Date >= $monthStart",
            ("$category", category), ("$monthStart", SqliteTextUtils.FormatDate(monthStart)));

        var spentSoFar = spentRows.Sum(r => -r.Amount);
        var remaining = monthlyLimit - spentSoFar;
        var percentUsed = monthlyLimit == 0 ? 0d : (double)(spentSoFar / monthlyLimit * 100);

        return new BudgetStatus(category, monthlyLimit, spentSoFar, remaining, percentUsed);
    }

    public async Task<IReadOnlyList<CategoryTotal>> GetTopCategories(DateTime startDate, DateTime endDate, int limit = 5)
    {
        var rows = await this.QueryRowsAsync(
            "AND Date >= $start AND Date < $end AND Amount < 0",
            ("$start", SqliteTextUtils.FormatDate(startDate)), ("$end", SqliteTextUtils.FormatDate(endDate.AddDays(1))));

        return rows
            .GroupBy(r => r.Category)
            .Select(g => new CategoryTotal(g.Key, g.Sum(r => -r.Amount)))
            .OrderByDescending(c => c.Total)
            .Take(limit)
            .ToList();
    }

    public async Task<CategorySpending> GetSpendingByCategory(DateTime startDate, DateTime endDate, string category)
    {
        var amountFilter = IncomeCategories.Contains(category) ? "AND Amount > 0" : "AND Amount < 0";
        var rows = await this.QueryRowsAsync(
            $"AND Date >= $start AND Date < $end AND Category = $category {amountFilter}",
            ("$start", SqliteTextUtils.FormatDate(startDate)), ("$end", SqliteTextUtils.FormatDate(endDate.AddDays(1))),
            ("$category", category));

        if (rows.Count == 0)
        {
            return new CategorySpending(category, 0, 0, 0);
        }

        var total = rows.Sum(r => Math.Abs(r.Amount));
        return new CategorySpending(category, total, rows.Count, total / rows.Count);
    }

    public async Task<IReadOnlyList<MerchantTotal>> GetSpendingByMerchant(DateTime startDate, DateTime endDate, int limit = 10)
    {
        var rows = await this.QueryRowsAsync(
            "AND Date >= $start AND Date < $end AND Amount < 0",
            ("$start", SqliteTextUtils.FormatDate(startDate)), ("$end", SqliteTextUtils.FormatDate(endDate.AddDays(1))));

        return rows
            .GroupBy(r => r.Merchant)
            .Select(g => new MerchantTotal(g.Key, g.Sum(r => -r.Amount)))
            .OrderByDescending(m => m.Total)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<SubscriptionInfo>> DetectSubscriptions(bool onlyForgotten = false, int minInactiveDays = 60)
    {
        var rows = await this.QueryRowsAsync("AND Recurring = 1 AND Amount < 0");
        if (rows.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var subscriptions = rows
            .GroupBy(r => r.Merchant)
            .Select(g =>
            {
                var lastPayment = g.Max(r => r.Date);
                var amounts = g.Select(r => -r.Amount).OrderBy(a => a).ToList();
                var daysInactive = (int)(now - lastPayment).TotalDays;
                return new SubscriptionInfo(g.Key, lastPayment, Median(amounts), g.Count(), g.First().Category, daysInactive);
            })
            .ToList();

        if (!onlyForgotten)
        {
            return subscriptions.OrderByDescending(s => s.LastPaymentDate).ToList();
        }

        return subscriptions
            .Where(s => s.DaysInactive >= minInactiveDays && s.PaymentCount >= 2)
            .OrderByDescending(s => s.DaysInactive)
            .ToList();
    }

    public async Task<SpendingPatternStats> AnalyzeSpendingPattern(string category)
    {
        var rows = await this.QueryRowsAsync("AND Category = $category AND Amount < 0", ("$category", category));
        if (rows.Count == 0)
        {
            return new SpendingPatternStats(category, 0, 0, 0, 0, [], 0, 0);
        }

        var total = rows.Sum(r => -r.Amount);
        var morningCount = rows.Count(r => r.Date.Hour < 12);
        var weekdayCount = rows.Count(r => r.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));

        var monthlyTotals = rows
            .GroupBy(r => new { r.Date.Year, r.Date.Month })
            .Select(g => g.Sum(r => -r.Amount))
            .ToList();

        var topMerchants = rows
            .GroupBy(r => r.Merchant)
            .Select(g => new MerchantTotal(g.Key, g.Sum(r => -r.Amount)))
            .OrderByDescending(m => m.Total)
            .Take(3)
            .ToList();

        return new SpendingPatternStats(
            category, monthlyTotals.Average(), monthlyTotals.Count,
            (double)morningCount / rows.Count * 100, (double)weekdayCount / rows.Count * 100,
            topMerchants, total, rows.Count);
    }

    public async Task<SpendingByHourStats> AnalyzeSpendingByHour(string category, int afterHour)
    {
        var rows = await this.QueryRowsAsync("AND Category = $category AND Amount < 0", ("$category", category));
        if (rows.Count == 0)
        {
            return new SpendingByHourStats(category, afterHour, 0, 0, 0, 0, 0, 0, 0);
        }

        var afterRows = rows.Where(r => r.Date.Hour >= afterHour).ToList();
        var otherRows = rows.Where(r => r.Date.Hour < afterHour).ToList();

        var totalSpend = rows.Sum(r => -r.Amount);
        var afterSpend = afterRows.Sum(r => -r.Amount);

        return new SpendingByHourStats(
            category, afterHour,
            rows.Count, afterRows.Count, (double)afterRows.Count / rows.Count * 100,
            totalSpend, afterSpend,
            afterRows.Count > 0 ? afterSpend / afterRows.Count : 0,
            otherRows.Count > 0 ? otherRows.Sum(r => -r.Amount) / otherRows.Count : 0);
    }

    public async Task<WeekendSpendingStats> AnalyzeWeekendSpending()
    {
        var rows = await this.QueryRowsAsync("AND Amount < 0");
        var weekday = rows.Where(r => r.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
        var weekend = rows.Where(r => r.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToList();

        var weekdayTotal = weekday.Sum(r => -r.Amount);
        var weekendTotal = weekend.Sum(r => -r.Amount);
        var weekdayAvg = weekday.Count > 0 ? weekdayTotal / weekday.Count : 0;
        var weekendAvg = weekend.Count > 0 ? weekendTotal / weekend.Count : 0;
        var spikePct = weekdayAvg != 0 ? (double)((weekendAvg - weekdayAvg) / weekdayAvg * 100) : 0;

        return new WeekendSpendingStats(weekdayAvg, weekendAvg, spikePct, weekdayTotal, weekendTotal, weekday.Count, weekend.Count);
    }

    public async Task<SavingsSimulation> SimulateSavings(string category, decimal reductionPct, string? merchant = null)
    {
        var where = "AND Category = $category AND Amount < 0";
        var parameters = new List<(string, object)> { ("$category", category) };
        if (merchant is not null)
        {
            where += " AND Merchant LIKE $merchant ESCAPE '\\'";
            parameters.Add(("$merchant", $"%{SqliteTextUtils.EscapeLike(merchant)}%"));
        }

        var rows = await this.QueryRowsAsync(where, parameters.ToArray());
        if (rows.Count == 0)
        {
            return new SavingsSimulation(category, reductionPct, 0, 0, 0, 0);
        }

        var monthlyTotals = rows
            .GroupBy(r => new { r.Date.Year, r.Date.Month })
            .Select(g => g.Sum(r => -r.Amount))
            .ToList();
        var monthlyAverage = monthlyTotals.Average();
        var monthlySaving = monthlyAverage * reductionPct / 100;

        return new SavingsSimulation(category, reductionPct, monthlyAverage, monthlySaving, monthlySaving * 12, monthlyTotals.Count);
    }

    public async Task<MonthEndProjection> ProjectMonthEndBalance()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var thisMonthRows = await this.QueryRowsAsync(
            "AND Date >= $monthStart AND Date <= $now",
            ("$monthStart", SqliteTextUtils.FormatDate(monthStart)), ("$now", SqliteTextUtils.FormatDate(now)));

        var income = thisMonthRows.Where(r => r.Amount > 0).Sum(r => r.Amount);
        var expenses = thisMonthRows.Where(r => r.Amount < 0).Sum(r => -r.Amount);

        var lastMonthEnd = monthStart.AddSeconds(-1);
        var lastMonthStart = new DateTime(lastMonthEnd.Year, lastMonthEnd.Month, 1);

        var lastMonthRecurring = await this.QueryRowsAsync(
            "AND Date >= $lastStart AND Date <= $lastEnd AND Recurring = 1",
            ("$lastStart", SqliteTextUtils.FormatDate(lastMonthStart)), ("$lastEnd", SqliteTextUtils.FormatDate(lastMonthEnd)));

        var paidThisMonth = thisMonthRows.Select(r => r.Merchant).ToHashSet();
        var notPosted = lastMonthRecurring.Where(r => !paidThisMonth.Contains(r.Merchant)).ToList();

        var upcomingExpenses = notPosted.Where(r => r.Amount < 0).ToList();
        var upcomingTotal = upcomingExpenses.Sum(r => -r.Amount);
        var upcomingItems = upcomingExpenses
            .GroupBy(r => r.Merchant)
            .Select(g => new MerchantTotal(g.Key, g.Sum(r => -r.Amount)))
            .ToList();

        var upcomingIncome = notPosted.Where(r => r.Amount > 0).Sum(r => r.Amount);
        var projectedNet = income + upcomingIncome - expenses - upcomingTotal;

        return new MonthEndProjection(
            monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), now,
            income, expenses, income - expenses,
            upcomingTotal, upcomingIncome, projectedNet, upcomingItems);
    }

    public async Task<IReadOnlyList<SuspiciousTransaction>> DetectSuspiciousTransactions()
    {
        var allExpenses = await this.QueryRowsAsync("AND Amount < 0 AND Recurring = 0");
        if (allExpenses.Count < 5)
        {
            return [];
        }

        var mean = allExpenses.Average(r => r.Amount);
        var variance = allExpenses.Sum(r => (r.Amount - mean) * (r.Amount - mean)) / (allExpenses.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);
        var threshold = mean - (2 * stdDev); // more negative = larger charge

        return allExpenses
            .Where(r => r.Account == "credit_card" && r.Amount < threshold)
            .OrderBy(r => r.Amount)
            .Select(r => new SuspiciousTransaction(r.Id, r.Date, r.Merchant, r.Amount, r.Category))
            .ToList();
    }

    public async Task<IReadOnlyList<ImpossibleTravelFlag>> DetectImpossibleTravel()
    {
        var rows = await this.QueryRowsAsync("");
        var sorted = rows.OrderBy(r => r.Date).ToList();

        var flags = new List<ImpossibleTravelFlag>();
        for (var i = 1; i < sorted.Count; i++)
        {
            var previous = sorted[i - 1];
            var current = sorted[i];
            if (previous.Country == current.Country)
            {
                continue;
            }

            var gap = current.Date - previous.Date;
            if (gap < TimeSpan.FromHours(24))
            {
                flags.Add(new ImpossibleTravelFlag(
                    previous.Id, current.Id, previous.Country, current.Country, previous.Date, current.Date, gap));
            }
        }

        return flags;
    }

    private static decimal Median(List<decimal> sortedValues)
    {
        var count = sortedValues.Count;
        return count % 2 == 1
            ? sortedValues[count / 2]
            : (sortedValues[(count / 2) - 1] + sortedValues[count / 2]) / 2;
    }

    private async Task<List<Row>> QueryRowsAsync(string whereClause, params (string Name, object Value)[] parameters)
    {
        await using var connection = await this.connectionFactory.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, Date, Merchant, Amount, Category, Account, Recurring, Country
            FROM Transactions
            WHERE Status = 'Posted' {whereClause};
            """;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var rows = new List<Row>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private static Row ReadRow(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
        reader.GetString(2),
        decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetInt64(6) == 1,
        reader.GetString(7));

    private readonly record struct Row(
        long Id, DateTime Date, string Merchant, decimal Amount, string Category, string Account, bool Recurring, string Country);
}
