using System.Globalization;
using Mcp.FinanceTracker.DataGen;

// Rolling 12-month window ending today, so the demo data stays "fresh"
// every time this is regenerated instead of drifting into the past.
const int seed = 41;
var end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
var start = end.AddMonths(-12);

var outputPath = Path.GetFullPath(args.Length > 0
    ? args[0]
    : Path.Combine("..", "Mcp.FinanceTracker.Data", "Seed", "transactions.csv"));

var rng = new Random(seed);
var transactions = new List<GeneratedTransaction>();

for (var day = start; day <= end; day = day.AddDays(1))
{
    transactions.AddRange(TransactionsForDay(rng, day));
}

AddMonthlyRecurring(rng, transactions, start, end);
AddCreditCardPayments(rng, transactions, start, end);
AddSuspicious(rng, transactions, start, end);

transactions.Sort((a, b) => a.Date.CompareTo(b.Date));

WriteCsv(outputPath, transactions);

Console.WriteLine($"Generated {transactions.Count} transactions -> {outputPath}");
Console.WriteLine("By category:");
foreach (var group in transactions.GroupBy(t => t.Category).OrderByDescending(g => g.Count()))
{
    Console.WriteLine($"  {group.Key}: {group.Count()}");
}

// ── Day-to-day spending ──────────────────────────────────────────────────

List<GeneratedTransaction> TransactionsForDay(Random r, DateOnly day)
{
    var result = new List<GeneratedTransaction>();
    var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    if (!weekend && r.NextDouble() < 0.85)
    {
        result.Add(MakeTx(r, day, Pick(r, MerchantCatalog.Coffee)));
    }

    if (r.NextDouble() < (weekend ? 0.65 : 0.45))
    {
        result.Add(MakeTx(r, day, Pick(r, MerchantCatalog.Groceries)));
    }

    if (r.NextDouble() < (weekend ? 0.45 : 0.20))
    {
        var m = r.NextDouble() < 0.6 ? Pick(r, MerchantCatalog.Delivery) : Pick(r, MerchantCatalog.Restaurants);
        result.Add(MakeTx(r, day, m));
    }

    if (!weekend && r.NextDouble() < 0.55)
    {
        result.Add(MakeTx(r, day, Pick(r, MerchantCatalog.Transport)));
    }

    if (r.NextDouble() < (weekend ? 0.18 : 0.05))
    {
        result.Add(MakeTx(r, day, Pick(r, MerchantCatalog.Entertainment)));
    }

    if (r.NextDouble() < 0.10)
    {
        result.Add(MakeTx(r, day, Pick(r, MerchantCatalog.Shopping)));
    }

    if (r.NextDouble() < 0.04)
    {
        result.Add(MakeTx(r, day, Pick(r, MerchantCatalog.Health)));
    }

    return result;
}

// ── Monthly recurring: salary, subscriptions, utilities ─────────────────

void AddMonthlyRecurring(Random r, List<GeneratedTransaction> list, DateOnly rangeStart, DateOnly rangeEnd)
{
    var forgottenCutoff = rangeEnd.AddDays(-120);
    var cur = new DateOnly(rangeStart.Year, rangeStart.Month, 1);

    while (cur <= rangeEnd)
    {
        var salaryDay = new DateOnly(cur.Year, cur.Month, Math.Min(25, DateTime.DaysInMonth(cur.Year, cur.Month)));
        if (salaryDay >= rangeStart && salaryDay <= rangeEnd)
        {
            list.Add(new GeneratedTransaction(
                salaryDay.ToDateTime(new TimeOnly(10, 0)), "Muster GmbH", 2400.00m, "EUR",
                "salary", Accounts.Debit, true, Countries.Home, "Posted"));
        }

        foreach (var m in MerchantCatalog.SubscriptionsActive)
        {
            var day = new DateOnly(cur.Year, cur.Month, r.Next(1, 29));
            if (day >= rangeStart && day <= rangeEnd)
            {
                list.Add(MakeTx(r, day, m));
            }
        }

        foreach (var m in MerchantCatalog.SubscriptionsForgotten)
        {
            var day = new DateOnly(cur.Year, cur.Month, r.Next(1, 29));
            if (day >= rangeStart && day <= rangeEnd && day <= forgottenCutoff)
            {
                list.Add(MakeTx(r, day, m));
            }
        }

        foreach (var m in MerchantCatalog.Utilities)
        {
            var day = new DateOnly(cur.Year, cur.Month, r.Next(5, 16));
            if (day >= rangeStart && day <= rangeEnd)
            {
                list.Add(MakeTx(r, day, m));
            }
        }

        cur = cur.AddMonths(1);
    }
}

// ── Credit card: minimum payment 2 months out of 3, full payment 1 out of 3 ─

void AddCreditCardPayments(Random r, List<GeneratedTransaction> list, DateOnly rangeStart, DateOnly rangeEnd)
{
    var cur = new DateOnly(rangeStart.Year, rangeStart.Month, 1);
    var monthIndex = 0;

    while (cur <= rangeEnd)
    {
        var payDay = new DateOnly(cur.Year, cur.Month, Math.Min(14, DateTime.DaysInMonth(cur.Year, cur.Month)));
        if (payDay >= rangeStart && payDay <= rangeEnd)
        {
            var isMinimum = monthIndex % 3 != 2;
            var amount = isMinimum ? 50.00m : (decimal)(380 + r.NextDouble() * 140);
            list.Add(new GeneratedTransaction(
                payDay.ToDateTime(new TimeOnly(14, 0)), "Credit Card Payment", Math.Round(-amount, 2), "EUR",
                "credit_payment", Accounts.Debit, false, Countries.Home, "Posted"));
        }

        monthIndex++;
        cur = cur.AddMonths(1);
    }
}

// ── Two fraud-like foreign transactions, for DetectSuspiciousTransactions/
// DetectImpossibleTravel to find ──────────────────────────────────────────

void AddSuspicious(Random r, List<GeneratedTransaction> list, DateOnly rangeStart, DateOnly rangeEnd)
{
    var totalDays = rangeEnd.DayNumber - rangeStart.DayNumber;
    for (var i = 0; i < 2; i++)
    {
        var day = rangeStart.AddDays(r.Next(0, totalDays + 1));
        list.Add(MakeTx(r, day, Pick(r, MerchantCatalog.SuspiciousForeign)));
    }
}

// ── Shared helpers ────────────────────────────────────────────────────────

GeneratedTransaction MakeTx(Random r, DateOnly day, Merchant m)
{
    var amount = RandomAmount(r, m.AmountMin, m.AmountMax);
    var hour = PickHour(r, m);
    var minute = r.Next(0, 60);
    var timestamp = day.ToDateTime(new TimeOnly(hour, minute));
    return new GeneratedTransaction(
        timestamp, m.Name, Math.Round(-amount, 2), "EUR", m.Category, m.Account, m.Recurring, m.Country, "Posted");
}

decimal RandomAmount(Random r, double low, double high)
{
    if (low == high)
    {
        return (decimal)low;
    }

    var val = low + r.NextDouble() * (high - low);
    if (low > 5)
    {
        val = Math.Round(val * 2) / 2; // bias toward .00/.50 amounts for larger purchases
    }

    return Math.Round((decimal)val, 2);
}

int PickHour(Random r, Merchant m) =>
    m.HourBias is { Length: > 0 } bias ? bias[r.Next(bias.Length)] : r.Next(8, 23);

T Pick<T>(Random r, IReadOnlyList<T> list) => list[r.Next(list.Count)];

void WriteCsv(string path, List<GeneratedTransaction> list)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var writer = new StreamWriter(path, append: false);
    writer.WriteLine("date,merchant,amount,currency,category,account,recurring,country,status");
    foreach (var t in list)
    {
        writer.WriteLine(string.Join(',',
            t.Date.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            t.Merchant,
            t.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            t.Currency,
            t.Category,
            t.Account,
            t.Recurring ? "True" : "False",
            t.Country,
            t.Status));
    }
}
