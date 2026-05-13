using BeDemo.Api.Scripts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

namespace BeDemo.Api.Tests;

public class ReferenceSeedOptionsTests
{
    [Fact]
    public void ShouldSeedReferenceDataViaApi_Testing_AlwaysTrue()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Testing");
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = "true",
        }!).Build();

        Assert.True(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void ShouldSeedReferenceDataViaApi_Development_FalseWhenAssumeExternalSql()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ReferenceSeedOptions.AssumeExternalSqlReferenceAppliedKey] = "true",
        }!).Build();

        Assert.False(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }

    [Fact]
    public void ShouldSeedReferenceDataViaApi_Development_TrueWhenNotAssumed()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();

        Assert.True(ReferenceSeedOptions.ShouldSeedReferenceDataViaApi(env.Object, cfg));
    }
}
