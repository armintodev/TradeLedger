namespace TradeLedger.Core.Domain;

public static class TradePlanMatcher
{
    public static TradePlan? SelectFor(Trade trade, IEnumerable<TradePlan> candidates) =>
        candidates
            .Where(plan => plan.CanMatch(trade))
            .OrderByDescending(plan => plan.CreatedAt)
            .FirstOrDefault();

    public static bool Link(Trade trade, IEnumerable<TradePlan> candidates)
    {
        var plan = SelectFor(trade, candidates);

        if (plan is null)
        {
            trade.MarkUnplanned();

            return false;
        }

        plan.LinkTo(trade);
        trade.AdoptPlan(plan);

        return true;
    }
}
