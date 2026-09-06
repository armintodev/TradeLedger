namespace TradeLedger.Core.Domain;

public sealed class TaxonomyTerm : IUserOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public TaxonomyKind Kind { get; set; }
    public required string Name { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ColorHex { get; set; }
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TradeMistake : IUserOwned
{
    public Guid UserId { get; set; }
    public Guid TradeId { get; set; }
    public Trade? Trade { get; set; }
    public Guid TaxonomyTermId { get; set; }
    public TaxonomyTerm? Term { get; set; }

    public decimal? EstimatedCost { get; set; }

    public string? Note { get; set; }
}

public sealed class TradeTracking : IUserOwned
{
    public Guid UserId { get; set; }
    public Guid TradeId { get; set; }
    public Trade? Trade { get; set; }
    public Guid TaxonomyTermId { get; set; }
    public TaxonomyTerm? Term { get; set; }
    public string? Note { get; set; }
}
