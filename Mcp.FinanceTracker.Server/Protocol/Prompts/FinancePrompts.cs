using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Mcp.FinanceTracker.Server.Protocol.Prompts;

[McpServerPromptType]
public sealed class FinancePrompts
{
    [McpServerPrompt(Name = "monthly_review")]
    [Description(
        "Generate a structured monthly financial review: balance, top spending categories, budget status, " +
        "forgotten subscriptions, weekend vs weekday habits, and a month-end projection.")]
    public string MonthlyReview(
        [Description("Calendar month to review, format YYYY-MM. Defaults to the current month if omitted.")] string? month = null)
    {
        var (monthStart, monthEnd) = ResolveMonth(month);
        var startText = monthStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endText = monthEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"""
            Please put together my financial review for {monthStart:yyyy-MM} — covering the period from
            {startText} to {endText}.

            Cover: my overall balance, where the money went this month (top spending categories), how I'm
            tracking against any budgets I've set, whether any subscriptions look forgotten, how weekday
            spending compares to weekends, and a projection of where the month will land.

            Structure the answer under short headings with concrete numbers, not raw tool output — 2-3
            sentences per section.
            """;
    }

    [McpServerPrompt(Name = "investigate_suspicious_charge")]
    [Description(
        "Investigate the credit card for potential fraud: statistical outliers and impossible-travel patterns, " +
        "optionally scoped to one month.")]
    public string InvestigateSuspiciousCharge(
        [Description("Optional calendar month to focus on, format YYYY-MM. If omitted, checks the full transaction history.")] string? month = null)
    {
        var scope = string.IsNullOrWhiteSpace(month)
            ? "across my full transaction history"
            : FormatScopedInstruction(month);

        return $"""
            I'm concerned about possible fraud on my credit card. Please investigate {scope}, using the fraud
            detectors available to you (statistical outliers in card spending, and impossible-travel patterns
            across transactions).

            Explain what you find calmly and factually — never state that a transaction is confirmed fraud, only
            that it looks unusual and worth reviewing. If anything looks suspicious, tell me to contact my card
            issuer's support directly rather than trying to reverse or dispute the transaction yourself.
            """;
    }

    private static string FormatScopedInstruction(string month)
    {
        var (monthStart, monthEnd) = ResolveMonth(month);
        var startText = monthStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endText = monthEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"for {monthStart:yyyy-MM} — transactions dated between {startText} and {endText}";
    }

    private static (DateTime MonthStart, DateTime MonthEnd) ResolveMonth(string? month)
    {
        month = month?.Trim();
        if (string.IsNullOrEmpty(month))
        {
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            return (currentMonthStart, currentMonthStart.AddMonths(1).AddDays(-1));
        }

        if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthStart))
        {
            throw new McpException($"Invalid month '{month}'. Expected format YYYY-MM.");
        }

        return (monthStart, monthStart.AddMonths(1).AddDays(-1));
    }
}
