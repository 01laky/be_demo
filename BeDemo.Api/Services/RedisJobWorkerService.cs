using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BeDemo.Api.Services;

/// <summary>
/// Background worker: promotes delayed jobs, then processes ready queue (FIFO).
/// </summary>
public sealed class RedisJobWorkerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisJobWorkerService> _logger;
    private readonly RedisJobWorkerOptions _options;

    public RedisJobWorkerService(
        IConnectionMultiplexer redis,
        ILogger<RedisJobWorkerService> logger,
        IOptions<RedisJobWorkerOptions> options)
    {
        _redis = redis;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Redis job worker started (poll {PollMs} ms)", _options.PollMilliseconds);
        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PromoteDelayedAsync(db, stoppingToken);
                var raw = await db.ListRightPopAsync(RedisJobQueue.ReadyListKey);
                if (raw.IsNullOrEmpty)
                {
                    await Task.Delay(_options.PollMilliseconds, stoppingToken);
                    continue;
                }

                await ProcessJobAsync(raw!, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis job worker loop error");
                await Task.Delay(_options.PollMilliseconds, stoppingToken);
            }
        }

        _logger.LogInformation("Redis job worker stopped");
    }

    private async Task PromoteDelayedAsync(IDatabase db, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var due = await db.SortedSetRangeByScoreAsync(
            RedisJobQueue.DelayedZsetKey,
            double.NegativeInfinity,
            now,
            take: 50);

        foreach (var member in due)
        {
            var removed = await db.SortedSetRemoveAsync(RedisJobQueue.DelayedZsetKey, member);
            if (removed)
                await db.ListLeftPushAsync(RedisJobQueue.ReadyListKey, member);
        }
    }

    private Task ProcessJobAsync(string json, CancellationToken ct)
    {
        RedisJobEnvelope? env;
        try
        {
            env = JsonSerializer.Deserialize<RedisJobEnvelope>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid job JSON, dropping");
            return Task.CompletedTask;
        }

        if (env == null)
            return Task.CompletedTask;

        switch (env.Type)
        {
            case "reel.postprocess":
                _logger.LogInformation(
                    "Processed reel.postprocess job {JobId} payload={Payload}",
                    env.Id,
                    env.Payload);
                break;
            default:
                _logger.LogWarning("Unknown job type {Type} id={Id}", env.Type, env.Id);
                break;
        }

        return Task.CompletedTask;
    }
}

public sealed class RedisJobWorkerOptions
{
    public int PollMilliseconds { get; set; } = 750;
}
