using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using ManyFaces.Push.V1;
using Microsoft.Extensions.Options;

namespace BeDemo.Api.Services;

/// <summary>
/// gRPC client for <see cref="PushService.PushServiceClient"/> (many_faces_push). Returns null from <see cref="IPushWorkerClient.SendPushAsync"/>
/// when <see cref="PushOptions.IsEnabled"/> is false so callers can skip work without exceptions.
/// </summary>
public sealed class PushWorkerGrpcClient : IPushWorkerClient, IDisposable
{
    private readonly IOptions<PushOptions> _options;
    private readonly ILogger<PushWorkerGrpcClient> _logger;
    private readonly GrpcChannel? _channel;
    private readonly PushService.PushServiceClient? _client;
    private readonly List<X509Certificate2> _tlsCertificatesToDispose = [];

    /// <summary>Builds a channel when push is enabled and the worker URL is valid.</summary>
    public PushWorkerGrpcClient(IOptions<PushOptions> options, ILogger<PushWorkerGrpcClient> logger)
    {
        _options = options;
        _logger = logger;
        var o = options.Value;
        if (!o.IsEnabled)
        {
            return;
        }

        _channel = GrpcWorkerChannelFactory.CreateChannel(GrpcWorkerChannelFactory.FromPush(o), _tlsCertificatesToDispose);
        _client = new PushService.PushServiceClient(_channel);
    }

    /// <inheritdoc />
    public async Task<SendPushResponse?> SendPushAsync(SendPushRequest request, CancellationToken cancellationToken = default)
    {
        var o = _options.Value;
        if (!o.IsEnabled || _client is null)
        {
            return null;
        }

        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(o.WorkerAuthToken))
        {
            // Header name matches many_faces_push internal/server/auth_interceptor.go.
            headers.Add("x-push-worker-token", o.WorkerAuthToken.Trim());
        }

        var deadlineSeconds = Math.Clamp(o.GrpcDeadlineSeconds, 1, 120);
        var callOptions = new CallOptions(headers, DateTime.UtcNow.AddSeconds(deadlineSeconds), cancellationToken);

        try
        {
            return await _client.SendPushAsync(request, callOptions);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Push worker SendPush failed: {Code} {Detail}", ex.StatusCode, ex.Status.Detail);
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _channel?.Dispose();
        foreach (var c in _tlsCertificatesToDispose)
        {
            c.Dispose();
        }

        _tlsCertificatesToDispose.Clear();
    }
}
