using Microsoft.EntityFrameworkCore;
using TradeLedger.Core.Domain;

namespace TradeLedger.Core.Persistence.Seed;

public static class TaxonomySeeder
{
    public static readonly string[] Strategies =
    [
        "All", "1 Touch", "2 Touch", "3 Touch", "Pre Breakout", "Risky Breakout",
        "Reaction", "Fakeout", "News Trading", "FOMO", "Pattern",
    ];

    public static readonly string[] MentalStates =
    [
        "Calm", "Angry", "Happy", "Stressfull", "Doubt", "Focused", "Not Focused",
        "Confidence", "Thrilled", "Asleep", "Tired", "Stoploss pressure", "Excited",
        "driving", "boredo",
    ];

    public static readonly string[] Mistakes =
    [
        "Fake Breakout", "Others signal", "Market Trend Analysis", "feeling",
        "Before Candle Close", "fomo", "S/R analyse", "First 5min",
    ];

    public static readonly string[] Trackings =
    [
        "Entry Point stop", "Closed by myself", "FOMO close", "Stopped",
    ];

    public static readonly string[] ChecklistItems =
    [
        "Total2", "BTC.D", "USDT.D", "Market trend", "SMA", "Market Session",
        "BTC Pair", "RSI", "Volume", "Candle Shape",
    ];

    public static readonly string[] Timeframes = ["15m", "1h", "4h", "1D"];

    public static readonly string[] EntryTypes = ["Futures", "Spot", "Wallet", "Liquidity Pool"];

    public static readonly string[] ExitTypes =
    [
        "Take Profit", "Stop Loss", "Manual", "Liquidation", "Partial", "Trailing",
    ];

    public static async Task SeedAsync(
        TradeLedgerDbContext db,
        Guid userId,
        CancellationToken ct = default)
    {
        var existing = await db.TaxonomyTerms
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId)
            .Select(t => new { t.Kind, t.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var present = existing
            .Select(t => (t.Kind, t.Name))
            .ToHashSet();

        var toAdd = new List<TaxonomyTerm>();

        void AddAll(TaxonomyKind kind, string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (present.Contains((kind, names[i])))
                {
                    continue;
                }

                toAdd.Add(TaxonomyTerm.Create(kind, names[i], sortOrder: i, userId: userId));
            }
        }

        AddAll(TaxonomyKind.Strategy, Strategies);
        AddAll(TaxonomyKind.MentalState, MentalStates);
        AddAll(TaxonomyKind.Mistake, Mistakes);
        AddAll(TaxonomyKind.Tracking, Trackings);
        AddAll(TaxonomyKind.ChecklistItem, ChecklistItems);
        AddAll(TaxonomyKind.Timeframe, Timeframes);
        AddAll(TaxonomyKind.EntryType, EntryTypes);
        AddAll(TaxonomyKind.ExitType, ExitTypes);

        if (toAdd.Count == 0)
        {
            return;
        }

        await db.TaxonomyTerms.AddRangeAsync(toAdd, ct);
        await db.SaveChangesAsync(ct);
    }
}
