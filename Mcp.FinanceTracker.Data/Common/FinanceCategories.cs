namespace Mcp.FinanceTracker.Data.Common;

public static class FinanceCategories
{
    public static readonly IReadOnlyList<string> All =
    [
        "coffee", "groceries", "restaurants", "delivery", "transport",
        "entertainment", "shopping", "health", "subscriptions",
        "utilities", "salary", "credit_payment", "travel",
    ];

    // Used to build SQL CHECK constraints — single source of truth shared with
    // the MCP tool parameter schemas defined in Mcp.FinanceTracker.Server.
    public static string AsSqlList() => string.Join(", ", All.Select(c => $"'{c}'"));
}
