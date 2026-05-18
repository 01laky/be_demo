using BeDemo.Api.Models;
using BeDemo.Api.Utils;
using FluentAssertions;

namespace BeDemo.Api.Tests;

public class OperatorModerationGuardTests
{
    [Fact]
    public void CanBanTarget_ShouldRejectSelfAndSuperAdmin()
    {
        var target = new ApplicationUser
        {
            Id = "target",
            UserRole = new UserRole { Name = UserRole.GlobalRoleNames.User },
        };
        OperatorModerationGuard.CanBanTarget("target", target).Should().BeFalse();
        var superTarget = new ApplicationUser
        {
            Id = "target",
            UserRole = new UserRole { Name = UserRole.GlobalRoleNames.SuperAdmin },
        };
        OperatorModerationGuard.CanBanTarget("operator", superTarget).Should().BeFalse();
        OperatorModerationGuard.CanBanTarget("operator", target).Should().BeTrue();
    }
}
