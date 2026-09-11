namespace TradeLedger.Core.Domain;

public sealed class TaxonomyTerm : IUserOwned
{
    private TaxonomyTerm()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public TaxonomyKind Kind { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string? ColorHex { get; private set; }

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static TaxonomyTerm Create(
        TaxonomyKind kind,
        string name,
        int sortOrder = 0,
        string? colorHex = null,
        string? description = null,
        Guid userId = default) => new()
    {
        UserId = userId,
        Kind = kind,
        Name = Guard.NotBlank(name, nameof(name)),
        SortOrder = sortOrder,
        ColorHex = colorHex,
        Description = description,
    };

    public void Rename(string name)
    {
        Name = Guard.NotBlank(name, nameof(name));
    }

    public void Describe(string? description)
    {
        Description = description;
    }

    public void Recolor(string? colorHex)
    {
        ColorHex = colorHex;
    }

    public void Reorder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate()
    {
        IsActive = true;
    }
}

public sealed class TradeMistake : IUserOwned
{
    private TradeMistake()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TradeId { get; private set; }

    public Trade? Trade { get; private set; }

    public Guid TaxonomyTermId { get; private set; }

    public TaxonomyTerm? Term { get; private set; }

    public decimal? EstimatedCost { get; private set; }

    public string? Note { get; private set; }

    public static TradeMistake For(Trade trade, Guid taxonomyTermId, string? note = null) => new()
    {
        UserId = trade.UserId,
        TradeId = trade.Id,
        TaxonomyTermId = Guard.NotEmpty(taxonomyTermId, nameof(taxonomyTermId)),
        Note = note,
    };

    public void EstimateCost(decimal? cost)
    {
        EstimatedCost = cost;
    }

    public void Annotate(string? note)
    {
        Note = note;
    }
}

public sealed class TradeTracking : IUserOwned
{
    private TradeTracking()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TradeId { get; private set; }

    public Trade? Trade { get; private set; }

    public Guid TaxonomyTermId { get; private set; }

    public TaxonomyTerm? Term { get; private set; }

    public string? Note { get; private set; }

    public static TradeTracking For(Trade trade, Guid taxonomyTermId, string? note = null) => new()
    {
        UserId = trade.UserId,
        TradeId = trade.Id,
        TaxonomyTermId = Guard.NotEmpty(taxonomyTermId, nameof(taxonomyTermId)),
        Note = note,
    };

    public void Annotate(string? note)
    {
        Note = note;
    }
}
