namespace Mcp.FinanceTracker.Data.Models;

public sealed record Transaction(
    long Id,
    DateTime Date,
    string Merchant,
    decimal Amount,
    string Currency,
    string Category,
    string Account,
    bool Recurring,
    string Country,
    string Status);
