namespace TradeLedger.Core.Domain;

public sealed class Transfer : IUserOwned
{
    private readonly List<Attachment> _attachments = [];

    private Transfer()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid UserId { get; private set; }

    public Guid? FromAccountId { get; private set; }

    public Account? FromAccount { get; private set; }

    public Guid? ToAccountId { get; private set; }

    public Account? ToAccount { get; private set; }

    public TransferDirection Direction { get; private set; }

    public string Asset { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public decimal Fee { get; private set; }

    public decimal? ValueUsd { get; private set; }

    public bool WriteOff { get; private set; }

    public string? Network { get; private set; }

    public string? TxHash { get; private set; }

    public string? Counterparty { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public IReadOnlyCollection<Attachment> Attachments => _attachments;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public decimal NetAmount => Amount - Fee;

    public static Transfer Record(NewTransfer spec)
    {
        var transfer = new Transfer
        {
            UserId = spec.UserId,
            FromAccountId = spec.FromAccountId,
            ToAccountId = spec.ToAccountId,
            Direction = spec.Direction,
            Asset = Guard.NotBlank(spec.Asset, nameof(spec.Asset)).ToUpperInvariant(),
            Amount = Guard.Positive(spec.Amount, nameof(spec.Amount)),
            Fee = Guard.NotNegative(spec.Fee, nameof(spec.Fee)),
            ValueUsd = spec.ValueUsd,
            WriteOff = spec.WriteOff,
            Network = spec.Network,
            TxHash = spec.TxHash,
            Counterparty = spec.Counterparty,
            Note = spec.Note,
            OccurredAt = Guard.NotDefault(spec.OccurredAt, nameof(spec.OccurredAt)),
        };

        switch (transfer.Direction)
        {
            case TransferDirection.Deposit:
                Guard.Rule(
                    transfer.ToAccountId is not null,
                    "deposit_needs_destination",
                    "A deposit must name the account the money landed in.");

                break;

            case TransferDirection.Withdrawal:
                Guard.Rule(
                    transfer.FromAccountId is not null,
                    "withdrawal_needs_source",
                    "A withdrawal must name the account the money left.");

                break;

            case TransferDirection.Internal:
                Guard.Rule(
                    transfer.FromAccountId is not null && transfer.ToAccountId is not null,
                    "internal_needs_both_sides",
                    "An internal move must name both the source and the destination account.");

                Guard.Rule(
                    transfer.FromAccountId != transfer.ToAccountId,
                    "internal_same_account",
                    "An internal move must be between two different accounts.");

                break;

            default:
                throw new DomainValidationException(
                    nameof(spec.Direction),
                    "Unknown transfer direction.");
        }

        return transfer;
    }

    public void MarkWrittenOff(string? note = null)
    {
        WriteOff = true;
        Note = note ?? Note;
    }

    public void Annotate(string? note)
    {
        Note = note;
    }
}

public sealed record NewTransfer
{
    public Guid UserId { get; init; }

    public Guid? FromAccountId { get; init; }

    public Guid? ToAccountId { get; init; }

    public required TransferDirection Direction { get; init; }

    public required string Asset { get; init; }

    public required decimal Amount { get; init; }

    public decimal Fee { get; init; }

    public decimal? ValueUsd { get; init; }

    public bool WriteOff { get; init; }

    public string? Network { get; init; }

    public string? TxHash { get; init; }

    public string? Counterparty { get; init; }

    public string? Note { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
