using BeDemo.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BeDemo.Api.Tests;

/// <summary>
/// Ensures the push gRPC client is a safe no-op when <see cref="PushOptions.Enabled"/> is false (default CI / laptops).
/// </summary>
public sealed class PushWorkerGrpcClientTests
{
    [Fact]
    public async Task SendPushAsync_WhenDisabled_ReturnsNull()
    {
        var options = Options.Create(new PushOptions { Enabled = false, WorkerGrpcUrl = "http://localhost:59999" });
        using var sut = new PushWorkerGrpcClient(options, NullLogger<PushWorkerGrpcClient>.Instance);
        var resp = await sut.SendPushAsync(new ManyFaces.Push.V1.SendPushRequest());
        Assert.Null(resp);
    }
}
