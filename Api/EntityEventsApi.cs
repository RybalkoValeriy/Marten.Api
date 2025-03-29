using Marten.API.Events;
using Marten.API.Marten;
using Microsoft.AspNetCore.Mvc;

namespace Marten.API.API;

public static class EntityEventsApi
{
    public static RouteGroupBuilder MapEventsApi(this IEndpointRouteBuilder endpointRouteBuilder)
    {
        var api = endpointRouteBuilder.MapGroup("api/entity/");

        api.MapPost("/create",
            async ([FromBody] OnCreatedEntity @event,
                [FromServices] IDocumentSession documentSession,
                CancellationToken cancellationToken) =>
            {
                await documentSession.Events.WriteToAggregate<Entity>(
                    @event.Id,
                    stream => stream.AppendOne(@event),
                    cancellationToken);

                return Results.Ok(@event.Id);
            });

        api.MapPatch("/updateDescription",
            async ([FromBody] OnDescriptionUpdatedEntity @event,
                [FromServices] IDocumentSession documentSession,
                CancellationToken cancellationToken) =>
            {
                await documentSession
                    .Events
                    .WriteToAggregate<Entity>(
                        @event.Id, stream => stream.AppendOne(@event),
                        cancellationToken);

                return Results.Ok(@event.Id);
            });

        api.MapPut("/update",
            async ([FromBody] OnUpdatedEntity @event,
                [FromServices] IDocumentSession documentSession,
                CancellationToken cancellationToken) =>
            {
                await documentSession.Events.WriteToAggregate<Entity>(
                    @event.Id,
                    stream => stream.AppendOne(@event),
                    cancellationToken);

                return Results.Ok(@event.Id);
            });

        api.MapDelete("/events/{id:Guid}",
            async ([FromRoute] Guid id,
                [FromBody] OnDeletedEntity @event,
                [FromServices] IDocumentSession documentSession,
                CancellationToken cancellationToken) =>
            {
                await documentSession.Events.WriteToAggregate<Entity>(
                    @event.Id,
                    stream => stream.AppendOne(@event),
                    cancellationToken);

                return Results.Ok(id);
            });

        api.MapGet("/flat_views/audit_log/{id:Guid}",
            async ([FromRoute] Guid id, [FromServices] IDocumentSession documentSession) =>
            {
                var auditLog = await documentSession.LoadAsync<AuditLog>(id);
                return auditLog is null
                    ? Results.NotFound()
                    : Results.Ok(auditLog);
            });

        api.MapGet("/flat_views/audit_log",
            async ([FromServices] IDocumentSession documentSession) =>
            {
                var auditLog = await documentSession.Query<AuditLog>().ToListAsync();
                return Results.Ok(auditLog);
            });

        api.MapGet("/events/lastStatus/search/{phrase}",
            async ([FromRoute] string phrase, [FromServices] IDocumentSession documentSession) =>
            {
                var results = await documentSession
                    .Query<EntityStatus>()
                    .Where(x => x.Name.NgramSearch(phrase) || x.Description.NgramSearch(phrase)).ToListAsync();

                return Results.Ok(results);
            });

        api.MapGet("/events/lastStatus/{id:Guid}",
            async ([FromRoute] Guid id, [FromServices] IDocumentSession documentSession) =>
            {
                var result = await documentSession.LoadAsync<EntityStatus>(id);
                return result is null
                    ? Results.NotFound()
                    : Results.Ok(result);
            });

        api.MapGet("/events/root/{id:Guid}",
            async ([FromRoute] Guid id,
                [FromServices] IDocumentSession documentSession) =>
            {
                var categoryAggregateRoot = await documentSession.Events.AggregateStreamAsync<Entity>(id);

                return Results.Ok(categoryAggregateRoot);
            });

        api.MapGet("/events/event_store/{id:Guid}",
            async ([FromRoute] Guid id,
                [FromServices] IDocumentSession documentSession) =>
            {
                var events = await documentSession.GetEventsHistory(id);

                return Results.Ok(events.Select(e => new { e.EventTypeName, e.Data }).ToList());
            });

        return api;
    }
}