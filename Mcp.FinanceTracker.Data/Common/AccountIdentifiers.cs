namespace Mcp.FinanceTracker.Data.Common;

public static class AccountIdentifiers
{
    private const string MainDebitIban = "DE89370400440532013000";
    private const string CreditCardUriId = "card-4242";
    private const string CreditCardMasked = "**** **** **** 4242";

    public static readonly IReadOnlyList<string> All = ["main_debit", "credit_card"];

    public static string ToUriId(string account) => account switch
    {
        "main_debit" => MainDebitIban,
        "credit_card" => CreditCardUriId,
        _ => throw new ArgumentOutOfRangeException(nameof(account), account, "Unknown account."),
    };

    public static string ToDisplayId(string account) => account switch
    {
        "main_debit" => MainDebitIban,
        "credit_card" => CreditCardMasked,
        _ => throw new ArgumentOutOfRangeException(nameof(account), account, "Unknown account."),
    };

    public static string? ToAccountCode(string uriId) => uriId switch
    {
        MainDebitIban => "main_debit",
        CreditCardUriId => "credit_card",
        _ => null,
    };
}
