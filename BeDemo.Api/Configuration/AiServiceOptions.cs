namespace BeDemo.Api.Configuration;

/// <summary>Python AI worker gRPC client settings.</summary>
public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public string GrpcAddress { get; set; } = "http://ai-demo-dev:50051";

    /// <summary>When true, refresh host profile from the worker on backend startup.</summary>
    public bool HostProfileRefreshOnStartup { get; set; } = true;

    /// <summary>Max seconds to retry GetHostProfile during startup before giving up.</summary>
    public int HostProfileStartupTimeoutSeconds { get; set; } = 30;
}
