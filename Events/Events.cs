namespace Marten.API.Events;

public record OnCreatedEntity(
    Guid Id,
    string Name,
    string Description,
    string UserId,
    DateTime OccuredAt
);

public record OnUpdatedEntity(
    Guid Id,
    string Name,
    string Description,
    string UserId,
    DateTime OccuredAt
);

public record OnDescriptionUpdatedEntity(
    Guid Id,
    string Description,
    string UserId,
    DateTime OccuredAt
);

public record OnDeletedEntity(
    Guid Id,
    string UserId,
    DateTime OccuredAt
);

public record Entity(
    Guid Id,
    string Name,
    string Description,
    string UserId,
    DateTime OccuredAt,
    bool IsDeleted)
{
    public static Entity Create(OnCreatedEntity @event)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(@event.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(@event.Description);

        return new Entity(@event.Id, @event.Name, @event.Description, @event.UserId, @event.OccuredAt, false);
    }

    public Entity Apply(OnUpdatedEntity @event)
    {
        if (IsDeleted)
        {
            throw new ArgumentException("Current entity already removed");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(@event.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(@event.Description);

        return new Entity(Id: @event.Id, Name: @event.Name, Description: @event.Description, UserId: @event.UserId,
            OccuredAt: @event.OccuredAt, false);
    }

    public Entity Apply(OnDescriptionUpdatedEntity @event)
    {
        if (IsDeleted)
        {
            throw new ArgumentException("Current entity already removed");
        }

        return this with
        {
            Id = @event.Id,
            Description = @event.Description,
            UserId = @event.UserId,
            OccuredAt = @event.OccuredAt
        };
    }

    public Entity Apply(OnDeletedEntity deleted) =>
        this with
        {
            Id = deleted.Id,
            UserId = deleted.UserId,
            OccuredAt = deleted.OccuredAt,
            IsDeleted = true
        };
}