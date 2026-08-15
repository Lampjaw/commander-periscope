using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace CommanderPeriscope.Session;

/// <summary>
/// Resolves a URL the *server process itself* can use to connect back to its own SignalR hub.
///
/// <see cref="GamePage"/> and any similar component run their own server-side <see cref="Microsoft.AspNetCore.SignalR.Client.HubConnection"/>
/// (Blazor Server components execute on the server, not in the browser) — so building that connection's URL
/// from <c>NavigationManager.ToAbsoluteUri</c> is a latent bug: that gives the *browser's* externally-visible
/// address, not one the server can necessarily reach itself. It happens to work when the app is reached at the
/// same host:port it's actually listening on (e.g. plain local `dotnet run`), which is exactly why this went
/// unnoticed until a Docker deployment remapped the external port (5050 -&gt; 5000) and the server's self-connection
/// to "localhost:5050" started failing with connection-refused, since nothing inside the container listens there.
/// Any reverse-proxy deployment with a different external port would hit the same failure.
/// </summary>
public sealed class InternalHubUrlResolver(IServer server)
{
    /// <summary>
    /// The loopback base address Kestrel is actually bound to (wildcard hosts like <c>*</c>/<c>+</c>/<c>0.0.0.0</c>/<c>[::]</c>
    /// rewritten to <c>localhost</c>), or <see langword="null"/> if no address is available yet (e.g. a test host
    /// with no real listener) — callers should fall back to <c>NavigationManager</c> in that case.
    /// </summary>
    public Uri? ResolveBaseAddress()
    {
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        if (address is null)
        {
            return null;
        }

        var loopback = address
            .Replace("://*", "://localhost")
            .Replace("://+", "://localhost")
            .Replace("://0.0.0.0", "://localhost")
            .Replace("://[::]", "://localhost");

        return Uri.TryCreate(loopback, UriKind.Absolute, out var uri) ? uri : null;
    }
}
