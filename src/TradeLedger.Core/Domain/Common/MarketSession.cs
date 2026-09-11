namespace TradeLedger.Core.Domain;

[Flags]
public enum MarketSession
{
    None = 0,
    Tokyo = 1 << 0,
    London = 1 << 1,
    NewYork = 1 << 2,
}

public static class MarketSessionCalendar
{
    public static readonly TimeSpan TokyoOpen = TimeSpan.FromHours(0);
    public static readonly TimeSpan TokyoClose = TimeSpan.FromHours(9);
    public static readonly TimeSpan LondonOpen = TimeSpan.FromHours(7);
    public static readonly TimeSpan LondonClose = TimeSpan.FromHours(16);
    public static readonly TimeSpan NewYorkOpen = TimeSpan.FromHours(12);
    public static readonly TimeSpan NewYorkClose = TimeSpan.FromHours(21);

    public static MarketSession At(DateTimeOffset instant)
    {
        var time = instant.ToUniversalTime().TimeOfDay;

        var session = MarketSession.None;

        if (Covers(time, TokyoOpen, TokyoClose))
        {
            session |= MarketSession.Tokyo;
        }

        if (Covers(time, LondonOpen, LondonClose))
        {
            session |= MarketSession.London;
        }

        if (Covers(time, NewYorkOpen, NewYorkClose))
        {
            session |= MarketSession.NewYork;
        }

        return session;
    }

    public static bool IsOverlap(MarketSession session) =>
        session != MarketSession.None && (session & (session - 1)) != 0;

    public static string Describe(MarketSession session) => session switch
    {
        MarketSession.None => "Off hours",
        MarketSession.Tokyo => "Tokyo",
        MarketSession.London => "London",
        MarketSession.NewYork => "New York",
        MarketSession.Tokyo | MarketSession.London => "Tokyo / London overlap",
        MarketSession.London | MarketSession.NewYork => "London / New York overlap",
        _ => string.Join(" / ", Enum.GetValues<MarketSession>()
            .Where(v => v != MarketSession.None && session.HasFlag(v))
            .Select(v => v.ToString())),
    };

    private static bool Covers(TimeSpan time, TimeSpan open, TimeSpan close) =>
        open <= close
            ? time >= open && time < close
            : time >= open || time < close;
}
