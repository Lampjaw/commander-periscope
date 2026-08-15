using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CommanderPeriscope.Web.Tests.TestSupport;

/// <summary>Captures every "ScopedUpdate" a <see cref="HubConnection"/> receives, in arrival order, thread-safely.</summary>
public sealed class ReceivedMessages
{
    private readonly List<(string Type, JsonElement Payload)> _messages = [];
    private readonly object _lock = new();

    public static ReceivedMessages AttachTo(HubConnection connection)
    {
        var messages = new ReceivedMessages();
        connection.On<string, object>("ScopedUpdate", (type, payload) => messages.Add(type, (JsonElement)payload));
        return messages;
    }

    private void Add(string type, JsonElement payload)
    {
        lock (_lock)
        {
            _messages.Add((type, payload.Clone()));
        }
    }

    public IReadOnlyList<(string Type, JsonElement Payload)> Snapshot()
    {
        lock (_lock)
        {
            return [.. _messages];
        }
    }

    public bool Any(string type) => Snapshot().Any(m => m.Type == type);

    public JsonElement Last(string type) => Snapshot().Last(m => m.Type == type).Payload;

    /// <summary>Polls until <paramref name="predicate"/> is true or the timeout elapses — broadcasts arrive asynchronously over the wire, even against an in-memory TestServer.</summary>
    public async Task WaitUntilAsync(Func<ReceivedMessages, bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTime.UtcNow < deadline)
        {
            if (predicate(this))
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("Timed out waiting for the expected ScopedUpdate message.");
    }
}
