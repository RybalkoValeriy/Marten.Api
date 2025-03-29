using Marten.Events;

namespace Marten.API.Marten;

public static class DocumentSessionExtensions
{
    public static Task CreateEventStreamAsync<T>(
        this IDocumentSession documentSession,
        Guid id,
        object @event,
        CancellationToken ct)
        where T : class
    {
        documentSession.Events.StartStream<T>(id, @event);
        return documentSession.SaveChangesAsync(token: ct);
    }

    public static async Task GetAndUpdateSafeAsync<T>(this IDocumentSession documentSession,
        Guid id,
        object @event,
        CancellationToken ct) where T : class
    {
        var agg = await documentSession.Events.AggregateStreamAsync<T>(id, token: ct);

        if (agg is null)
        {
            throw new InvalidOperationException("Not found");
        }

        documentSession.Events.Append(id, @event);
    }

    public static Task GetAndUpdateAsync<T>(
        this IDocumentSession documentSession,
        Guid id,
        object @event,
        CancellationToken ct
    ) where T : class =>
        documentSession.Events.WriteToAggregate<T>(id, stream =>
            stream.AppendOne(@event), ct);

    public static Task<IReadOnlyList<IEvent>> GetEventsHistory(this IDocumentSession documentSession, Guid id) =>
        documentSession.Events.FetchStreamAsync(id);

    public static Task<IReadOnlyList<T>> NgramSearchAsync<T>(this IDocumentSession documentSession, string phrase)
        where T : class =>
        documentSession
            .Query<T>()
            .Where(x => x.NgramSearch(phrase))
            .ToListAsync();
}