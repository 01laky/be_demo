using ManyFaces.Push.V1;

namespace BeDemo.Api.Services;

/// <summary>
/// Narrow abstraction over the Go push worker so application code can be tested with fakes.
/// </summary>
public interface IPushWorkerClient
{
    /// <summary>
    /// Sends one logical notification to the given registration tokens using localization keys only.
    /// Returns null when push is disabled or misconfigured (callers should treat as a no-op / feature off).
    /// </summary>
    Task<SendPushResponse?> SendPushAsync(SendPushRequest request, CancellationToken cancellationToken = default);
}
