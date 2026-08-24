namespace Mcp.FinanceTracker.DataGen;

internal sealed record GeneratedTransaction(
    DateTime Date,
    string Merchant,
    decimal Amount,
    string Currency,
    string Category,
    string Account,
    bool Recurring,
    string Country,
    string Status);
