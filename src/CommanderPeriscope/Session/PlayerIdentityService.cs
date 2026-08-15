using Microsoft.JSInterop;

namespace CommanderPeriscope.Session;

/// <summary>
/// The browser-local player token described in the design doc: no accounts, a client-generated id persisted
/// in localStorage so a refresh or reconnect re-attaches to the same seat. Scoped per circuit, cached after
/// the first lookup so repeated calls in the same session don't round-trip to JS again.
/// </summary>
public sealed class PlayerIdentityService(IJSRuntime js)
{
    private const string StorageKey = "commanderPeriscope.playerId";
    private string? _playerId;

    public async Task<string> GetOrCreatePlayerIdAsync()
    {
        if (_playerId is not null)
        {
            return _playerId;
        }

        var existing = await js.InvokeAsync<string?>("commanderPeriscope.getLocalStorage", StorageKey);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            _playerId = existing;
            return _playerId;
        }

        var generated = Guid.NewGuid().ToString("N");
        await js.InvokeVoidAsync("commanderPeriscope.setLocalStorage", StorageKey, generated);
        _playerId = generated;
        return generated;
    }
}
