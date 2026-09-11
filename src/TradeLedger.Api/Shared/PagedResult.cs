namespace TradeLedger.Api.Shared;

public sealed record PagedResult<T>(List<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool HasMore => Page * PageSize < Total;
}
