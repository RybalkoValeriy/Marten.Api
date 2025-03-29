using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using Marten.Events.Projections.Flattened;
using Weasel.Postgresql.Tables;

namespace Marten.API.Events;

public record AuditLog(Guid Id, AuditLogEntry[] Entries);

public record AuditLogEntry(
    string EventType,
    DateTime OccuredAt,
    string Description,
    object? Metadata);

public class AuditLogProjection : SingleStreamProjection<AuditLog>
{
    public static AuditLog Create(OnCreatedEntity createdEntity) =>
        new(createdEntity.Id, [
            new AuditLogEntry(
                nameof(OnCreatedEntity),
                createdEntity.OccuredAt,
                $"The Entity {createdEntity.Id} was published by user:{createdEntity.UserId} at:{createdEntity.OccuredAt}",
                createdEntity)
        ]);

    public AuditLog Apply(OnUpdatedEntity updatedEntity, AuditLog current) =>
        current with
        {
            Entries = current.Entries.Union([
                new AuditLogEntry(
                    nameof(OnUpdatedEntity),
                    updatedEntity.OccuredAt,
                    $"The Entity updated: {current.Id} by user:{updatedEntity.UserId} at:{updatedEntity.OccuredAt}",
                    updatedEntity)
            ]).ToArray()
        };

    public AuditLog Apply(OnDescriptionUpdatedEntity updatedEntity, AuditLog current) =>
        current with
        {
            Entries = current.Entries.Union([
                new AuditLogEntry(
                    nameof(OnDescriptionUpdatedEntity),
                    updatedEntity.OccuredAt,
                    $"The Description: `{updatedEntity.Description}` was update version of Entity: {current.Id} by user:{updatedEntity.UserId} at:{updatedEntity.OccuredAt}",
                    updatedEntity)
            ]).ToArray()
        };

    public AuditLog Apply(OnDeletedEntity onDeletedEntity, AuditLog current) =>
        current with
        {
            Entries = current.Entries.Union([
                new AuditLogEntry(
                    nameof(OnDeletedEntity),
                    onDeletedEntity.OccuredAt,
                    $"The Entity: {current.Id} was deleted by user:{onDeletedEntity.UserId} at:{onDeletedEntity.OccuredAt}",
                    onDeletedEntity)
            ]).ToArray()
        };
}

public class EntityFlatView : FlatTableProjection
{
    public EntityFlatView() : base("entity_flat_view", SchemaNameSource.DocumentSchema)
    {
        Table.AddColumn<Guid>("id").AsPrimaryKey();
        Table.AddColumn<string>("user_id").AllowNulls();
        Table.AddColumn<DateTime>("occured_at").AllowNulls();
        Table.AddColumn<bool>("is_deleted").NotNull();
        Table.AddColumn<string>("name").AllowNulls();
        Table.AddColumn<string>("description").AllowNulls();

        Project<OnCreatedEntity>(map =>
        {
            map.Map(x => x.Name, "name").AllowNulls();
            map.Map(x => x.Description, "description").AllowNulls();
            map.Map(x => x.UserId, "user_id").AllowNulls();
            map.SetValue("is_deleted", "FALSE");
        });

        Project<OnDescriptionUpdatedEntity>(map =>
        {
            map.Map(x => x.Description, "description").AllowNulls();
            map.Map(x => x.UserId, "user_id").AllowNulls();
            map.SetValue("is_deleted", "FALSE");
        });

        Delete<OnDeletedEntity>();
    }
}

public class EntityStatus : IProjection
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public int Version { get; set; }
    public string UserId { get; set; }
    public DateTime OccuredAt { get; set; }
    public bool IsDeleted { get; set; }

    public void Apply(
        IDocumentOperations operations,
        IReadOnlyList<StreamAction> streams) =>
        ApplyAsync(operations, streams, CancellationToken.None).Wait();

    public async Task ApplyAsync(
        IDocumentOperations operations,
        IReadOnlyList<StreamAction> streams,
        CancellationToken cancellationToken)
    {
        foreach (var data in streams.SelectMany(x => x.Events).OrderBy(x => x.Sequence).Select(x => x.Data))
        {
            switch (data)
            {
                case OnCreatedEntity createdEntity:
                    operations.Store(
                        new EntityStatus
                        {
                            Id = createdEntity.Id,
                            Name = createdEntity.Name,
                            Description = createdEntity.Description,
                            OccuredAt = createdEntity.OccuredAt,
                            UserId = createdEntity.UserId,
                            IsDeleted = false,
                        });
                    break;

                case OnUpdatedEntity updated:
                    operations.Store(
                        new EntityStatus
                        {
                            Id = updated.Id,
                            Name = updated.Name,
                            Description = updated.Description,
                            OccuredAt = updated.OccuredAt,
                            UserId = updated.UserId,
                            IsDeleted = false,
                        });
                    break;

                case OnDescriptionUpdatedEntity @event:
                    var existingEntity = await operations.LoadAsync<EntityStatus>(@event.Id, cancellationToken);

                    if (existingEntity != null)
                    {
                        existingEntity.Description = @event.Description;
                        existingEntity.OccuredAt = @event.OccuredAt;
                        existingEntity.UserId = @event.UserId;

                        // Store the updated projection
                        operations.Store(existingEntity);
                    }

                    break;

                case OnDeletedEntity deleted:
                    operations.HardDelete<EntityStatus>(deleted.Id);
                    break;
            }
        }
    }
}

public class MyTable : Table
{
    public MyTable() : base("postgres.flat_my_table")
    {
        AddColumn<Guid>("id").AsPrimaryKey();
        AddColumn<string>("body").NotNull();
        AddColumn("message", "varchar(250)").NotNull();
        AddColumn<DateTimeOffset>("execution_time").NotNull();

        Indexes.Add(new IndexDefinition("idx_my_table_execution_time")
        {
            Columns = ["execution_time"]
        });
    }
}