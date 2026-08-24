using System.ComponentModel;
using Mcp.FinanceTracker.Data.Abstractions;
using Mcp.FinanceTracker.Data.Models;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Mcp.FinanceTracker.Server.Tools;

[McpServerToolType]
public sealed class FinanceQueryTools
{
    private readonly ITransactionRepository transactions;
    private readonly IFinanceAnalyticsQueries analytics;

    public FinanceQueryTools(ITransactionRepository transactions, IFinanceAnalyticsQueries analytics)
    {
        this.transactions = transactions;
        this.analytics = analytics;
    }

    [McpServerTool(Name = "get_transactions_in_period")]
    [Description(
        "Return transactions in a date range with optional filters. Use as the primary raw-data source for any " +
        "date-scoped question, or to find the most recent transaction(s) from a specific merchant by passing a " +
        "broad date range with a merchant filter and a small limit.")]
    public Task<IReadOnlyList<Transaction>> GetTransactionsInPeriod(
        [Description("Inclusive start date, YYYY-MM-DD.")] DateTime startDate,
        [Description("Inclusive end date, YYYY-MM-DD.")] DateTime endDate,
        [Description("Optional category filter.")] string? category = null,
        [Description("Optional account filter: 'main_debit' or 'credit_card'.")] string? account = null,
        [Description("Optional merchant substring filter, case-insensitive.")] string? merchant = null,
        [Description("Optional max number of transactions to return, most recent first.")] int? limit = null)
        => this.transactions.GetTransactionsInPeriod(startDate, endDate, category, account, merchant, limit);

    [McpServerTool(Name = "get_balance")]
    [Description("Return the current balance: the sum of all posted (non-reversed) transactions.")]
    public Task<decimal> GetBalance() => this.analytics.GetBalance();

    [McpServerTool(Name = "get_budget_status")]
    [Description(
        "Return the configured monthly budget for a category compared against actual spend so far this calendar " +
        "month. Fails if no budget has been set for the category yet — use set_budget first.")]
    public async Task<BudgetStatus> GetBudgetStatus(
        [Description("Category to check, e.g. 'coffee', 'delivery'.")] string category)
    {
        try
        {
            return await this.analytics.GetBudgetStatus(category);
        }
        catch (InvalidOperationException ex)
        {
            throw new McpException(ex.Message);
        }
    }

    [McpServerTool(Name = "get_top_categories")]
    [Description(
        "Return the highest-spending categories within a date range, ranked by total. Use for overview questions " +
        "like 'where do I spend the most?'.")]
    public Task<IReadOnlyList<CategoryTotal>> GetTopCategories(
        [Description("Inclusive start date, YYYY-MM-DD.")] DateTime startDate,
        [Description("Inclusive end date, YYYY-MM-DD.")] DateTime endDate,
        [Description("Max number of categories to return. Default 5.")] int limit = 5)
        => this.analytics.GetTopCategories(startDate, endDate, limit);

    [McpServerTool(Name = "get_spending_by_category")]
    [Description(
        "Return total amount, transaction count, and average transaction size for a single category within a date " +
        "range. Works for expense categories and income categories ('salary', 'credit_payment').")]
    public Task<CategorySpending> GetSpendingByCategory(
        [Description("Inclusive start date, YYYY-MM-DD.")] DateTime startDate,
        [Description("Inclusive end date, YYYY-MM-DD.")] DateTime endDate,
        [Description("Category to analyse.")] string category)
        => this.analytics.GetSpendingByCategory(startDate, endDate, category);

    [McpServerTool(Name = "get_spending_by_merchant")]
    [Description(
        "Return merchants ranked by total spend within a date range. Use to find which specific store or service " +
        "is the biggest cost driver.")]
    public Task<IReadOnlyList<MerchantTotal>> GetSpendingByMerchant(
        [Description("Inclusive start date, YYYY-MM-DD.")] DateTime startDate,
        [Description("Inclusive end date, YYYY-MM-DD.")] DateTime endDate,
        [Description("Max number of merchants to return. Default 10.")] int limit = 10)
        => this.analytics.GetSpendingByMerchant(startDate, endDate, limit);

    [McpServerTool(Name = "detect_subscriptions")]
    [Description(
        "Return recurring subscription charges found in the transaction history: merchant, amount, frequency, and " +
        "days since the last charge. Set onlyForgotten=true to surface subscriptions that have gone silent for an " +
        "unusually long time — likely forgotten or cancelled elsewhere but still assumed active.")]
    public Task<IReadOnlyList<SubscriptionInfo>> DetectSubscriptions(
        [Description("If true, only return subscriptions inactive for at least minInactiveDays. Default false (return all).")] bool onlyForgotten = false,
        [Description("Minimum days without a charge to count as forgotten. Default 60 (~2 missed monthly cycles).")] int minInactiveDays = 60)
        => this.analytics.DetectSubscriptions(onlyForgotten, minInactiveDays);

    [McpServerTool(Name = "analyze_spending_pattern")]
    [Description(
        "Analyse spending patterns for one category: monthly average, share of purchases made in the morning " +
        "(before 12:00) and on weekdays, and top merchants in that category. Use for questions about a routine or " +
        "habit tied to a specific category, e.g. coffee or delivery.")]
    public Task<SpendingPatternStats> AnalyzeSpendingPattern(
        [Description("Category to analyse, e.g. 'coffee'.")] string category)
        => this.analytics.AnalyzeSpendingPattern(category);

    [McpServerTool(Name = "analyze_spending_by_hour")]
    [Description(
        "Analyse spending in one category split by time of day: what share happens after a given hour, and how " +
        "spend/average order size compares to the rest of the day. Use for questions about late-night or " +
        "impulse spending in a specific category.")]
    public Task<SpendingByHourStats> AnalyzeSpendingByHour(
        [Description("Category to analyse, e.g. 'delivery'.")] string category,
        [Description("Hour of day (0-23) after which transactions are considered 'late'.")] int afterHour)
        => this.analytics.AnalyzeSpendingByHour(category, afterHour);

    [McpServerTool(Name = "analyze_weekend_spending")]
    [Description(
        "Compare average transaction size and total spend on weekdays vs. weekends, across all categories. Use " +
        "when the user asks why their spending is higher on certain days, or wants to understand weekend habits.")]
    public Task<WeekendSpendingStats> AnalyzeWeekendSpending() => this.analytics.AnalyzeWeekendSpending();

    [McpServerTool(Name = "simulate_savings")]
    [Description(
        "Calculate monthly and annual savings from reducing spend in a category by a percentage. To simulate " +
        "cancelling a single subscription or merchant entirely, pass merchant and reductionPct=100.")]
    public Task<SavingsSimulation> SimulateSavings(
        [Description("Category to reduce.")] string category,
        [Description("Percentage reduction to simulate, 1-100.")] decimal reductionPct,
        [Description("Optional merchant substring filter to scope the simulation to a single merchant.")] string? merchant = null)
        => this.analytics.SimulateSavings(category, reductionPct, merchant);

    [McpServerTool(Name = "project_month_end_balance")]
    [Description(
        "Project the estimated net cash flow to the end of the current calendar month, based on income and " +
        "expenses so far plus known recurring charges/income from last month that haven't posted yet this month.")]
    public Task<MonthEndProjection> ProjectMonthEndBalance() => this.analytics.ProjectMonthEndBalance();

    [McpServerTool(Name = "detect_suspicious_transactions")]
    [Description(
        "Flag credit-card transactions that are statistical outliers compared to normal spending — candidates for " +
        "fraud. Use when the user reports an unrecognised charge or asks about account security. Never state a " +
        "transaction is confirmed fraud; only surface it for the user to review.")]
    public Task<IReadOnlyList<SuspiciousTransaction>> DetectSuspiciousTransactions()
        => this.analytics.DetectSuspiciousTransactions();

    [McpServerTool(Name = "detect_impossible_travel")]
    [Description(
        "Flag pairs of chronologically adjacent transactions that occurred in different countries within an " +
        "implausibly short time window (under 24 hours) — a classic fraud signal. Use alongside " +
        "detect_suspicious_transactions when investigating a reported unrecognised charge.")]
    public Task<IReadOnlyList<ImpossibleTravelFlag>> DetectImpossibleTravel()
        => this.analytics.DetectImpossibleTravel();
}
