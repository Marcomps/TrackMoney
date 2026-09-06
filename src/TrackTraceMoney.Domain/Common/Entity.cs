namespace TrackTraceMoney.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }
}
