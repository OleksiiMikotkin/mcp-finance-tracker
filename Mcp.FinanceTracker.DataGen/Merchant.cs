namespace Mcp.FinanceTracker.DataGen;

internal static class Accounts
{
    public const string Debit = "main_debit";
    public const string Credit = "credit_card";
}

internal static class Countries
{
    public const string Home = "DE";
}

internal sealed record Merchant(
    string Name,
    string Category,
    double AmountMin,
    double AmountMax,
    string Account = Accounts.Debit,
    bool Recurring = false,
    string Country = Countries.Home,
    int[]? HourBias = null);
