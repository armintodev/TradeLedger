namespace TradeLedger.Core.Shared;

public interface IUserContext
{
    Guid? UserId { get; }
}

public sealed class FixedUserContext(Guid? userId) : IUserContext
{
    public Guid? UserId { get; } = userId;
}

public sealed class AmbientUserContext : IUserContext
{
    public Guid? UserId { get; private set; }

    public void Set(Guid? userId) => UserId = userId;
}
