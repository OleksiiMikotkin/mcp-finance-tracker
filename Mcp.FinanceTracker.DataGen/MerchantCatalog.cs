namespace Mcp.FinanceTracker.DataGen;

internal static class MerchantCatalog
{
    public static readonly IReadOnlyList<Merchant> Coffee =
    [
        new("Coffee Fellows", "coffee", 2.5, 4.5, HourBias: [7, 8, 9]),
        new("Balzac Coffee", "coffee", 3.0, 6.0, HourBias: [8, 9, 10]),
        new("Starbucks", "coffee", 2.5, 5.5, HourBias: [7, 8, 9]),
    ];

    public static readonly IReadOnlyList<Merchant> Groceries =
    [
        new("Lidl", "groceries", 15, 80),
        new("Aldi", "groceries", 20, 120),
        new("Rewe", "groceries", 25, 100),
        new("Kaufland", "groceries", 40, 180),
    ];

    public static readonly IReadOnlyList<Merchant> Restaurants =
    [
        new("Nordsee", "restaurants", 8, 18),
        new("Zur Linde", "restaurants", 25, 70),
        new("Currywurst Bude", "restaurants", 3, 6),
    ];

    public static readonly IReadOnlyList<Merchant> Delivery =
    [
        new("Lieferando", "delivery", 12, 35, HourBias: [12, 13, 19, 20, 21, 22, 23]),
        new("Wolt", "delivery", 10, 30, HourBias: [12, 13, 20, 21, 22, 23]),
        new("Uber Eats", "delivery", 15, 40, HourBias: [19, 20, 21, 22]),
    ];

    public static readonly IReadOnlyList<Merchant> Transport =
    [
        new("FREE NOW", "transport", 3, 12),
        new("Deutsche Bahn", "transport", 3, 10),
        new("BVG", "transport", 3.5, 3.5),
    ];

    public static readonly IReadOnlyList<Merchant> Entertainment =
    [
        new("CinemaxX", "entertainment", 8, 15),
        new("Steam", "entertainment", 10, 60),
        new("Thalia", "entertainment", 5, 25),
    ];

    public static readonly IReadOnlyList<Merchant> SubscriptionsActive =
    [
        new("Netflix", "subscriptions", 12, 12, Recurring: true),
        new("Spotify", "subscriptions", 5, 5, Recurring: true),
        new("Apple One", "subscriptions", 15, 15, Recurring: true),
        new("iCloud Storage", "subscriptions", 3, 3, Recurring: true),
    ];

    public static readonly IReadOnlyList<Merchant> SubscriptionsForgotten =
    [
        new("FitX", "subscriptions", 15, 15, Recurring: true),
    ];

    public static readonly IReadOnlyList<Merchant> Utilities =
    [
        new("Vattenfall", "utilities", 40, 90, Recurring: true),
        new("Stadtwerke", "utilities", 15, 30, Recurring: true),
        new("Telekom", "utilities", 12, 12, Recurring: true),
        new("Vodafone", "utilities", 8, 8, Recurring: true),
    ];

    public static readonly IReadOnlyList<Merchant> Shopping =
    [
        new("MediaMarkt", "shopping", 20, 250),
        new("Zalando", "shopping", 40, 400),
        new("Zara", "shopping", 30, 180),
        new("C&A", "shopping", 25, 120),
    ];

    public static readonly IReadOnlyList<Merchant> Health =
    [
        new("Apotheke am Markt", "health", 5, 50),
        new("Hausarztpraxis Dr. Müller", "health", 40, 200),
    ];

    // Foreign Country on purpose — this is what DetectImpossibleTravel (M1) flags.
    public static readonly IReadOnlyList<Merchant> SuspiciousForeign =
    [
        new("Booking.com", "travel", 300, 900, Account: Accounts.Credit, Country: "NL"),
        new("AliExpress", "shopping", 150, 400, Account: Accounts.Credit, Country: "CN"),
    ];
}
