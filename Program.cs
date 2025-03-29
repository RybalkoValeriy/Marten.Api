using Marten;
using Marten.API.API;
using Marten.API.Events;
using Marten.Events.Daemon.Resiliency;
using Marten.Events.Projections;
using Weasel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMarten(o =>
    {
        o.DatabaseSchemaName = "postgres";
        o.Connection("User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=postgres;Pooling=true;");

        o.Events.AddEventType<OnCreatedEntity>();
        o.Events.AddEventType<OnUpdatedEntity>();
        o.Events.AddEventType<OnDescriptionUpdatedEntity>();
        o.Events.AddEventType<OnDeletedEntity>();

        o.Projections.Add(new EntityStatus(), ProjectionLifecycle.Async);
        o.Projections.Add<EntityFlatView>(ProjectionLifecycle.Async);
        o.Projections.Add<AuditLogProjection>(ProjectionLifecycle.Async);

        o.Events.MetadataConfig.CausationIdEnabled = true;
        o.Events.MetadataConfig.CorrelationIdEnabled = true;
        o.Events.MetadataConfig.HeadersEnabled = true;

        o.Projections.RebuildErrors.SkipApplyErrors = false;
        o.Projections.RebuildErrors.SkipSerializationErrors = false;
        o.Projections.RebuildErrors.SkipUnknownEvents = false;

        o.Policies.ForAllDocuments(m =>
        {
            m.Metadata.CorrelationId.Enabled = true;
            m.Metadata.LastModifiedBy.Enabled = true;
        });


        o.Schema.For<EntityStatus>()
            .NgramIndex(x => x.Name)
            .NgramIndex(x => x.Description);

        // add absolutely custom table
        o.Storage.ExtendedSchemaObjects.Add(new MyTable());

        // Marten will create any new objects that are missing,
        // attempt to update tables if it can, but drop and replace
        // tables that it cannot patch.
        o.AutoCreateSchemaObjects = AutoCreate.All;

        // // Marten will create any new objects that are missing or
        // // attempt to update tables if it can. Will *never* drop
        // // any existing objects, so no data loss
        // o.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        //
        //
        // // Marten will create missing objects on demand, but
        // // will not change any existing schema objects
        // o.AutoCreateSchemaObjects = AutoCreate.CreateOnly;
    })
    .ApplyAllDatabaseChangesOnStartup()
    .AddAsyncDaemon(DaemonMode.Solo)
    ;

// builder.Services.AddSingleton<IMartenService, MartenService>();
// builder.Services.AddTransient<CategoryRepository, CategoryRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapEventsApi();

app.Run();