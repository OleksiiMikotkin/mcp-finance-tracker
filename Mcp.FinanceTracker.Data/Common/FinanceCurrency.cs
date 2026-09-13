namespace Mcp.FinanceTracker.Data.Common;

public static class FinanceCurrency
{
    // The ledger is single-currency by design — every analytics aggregate sums Amount directly
    // with no conversion, so a mixed-currency ledger would silently produce meaningless totals.
    public const string Base = "EUR";
}
