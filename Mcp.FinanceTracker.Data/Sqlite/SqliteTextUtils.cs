using System.Globalization;

namespace Mcp.FinanceTracker.Data.Sqlite;

internal static class SqliteTextUtils
{
    public static string FormatDate(DateTime date) =>
        date.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    public static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
