using BeDemo.Api.Services;
using ManyFaces.Mailer.V1;

namespace BeDemo.Api.Tests;

public sealed class CapturingMailerWorkerClient : IMailerWorkerClient
{
    public SendTemplatedEmailRequest? LastRequest { get; private set; }

    public void Reset() => LastRequest = null;

    public Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
        SendTemplatedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult<SendTemplatedEmailResponse?>(new SendTemplatedEmailResponse { CorrelationId = "test-corr" });
    }

    public void Dispose()
    {
    }
}

/// <summary>Simulates mail worker disabled — <see cref="SendTemplatedEmailAsync"/> returns null.</summary>
public sealed class DisabledMailerWorkerClient : IMailerWorkerClient
{
    public Task<SendTemplatedEmailResponse?> SendTemplatedEmailAsync(
        SendTemplatedEmailRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<SendTemplatedEmailResponse?>(null);

    public void Dispose()
    {
    }
}
