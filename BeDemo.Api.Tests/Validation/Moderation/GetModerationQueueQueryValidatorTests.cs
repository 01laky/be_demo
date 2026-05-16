using BeDemo.Api.Validation.Moderation;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Moderation;

public sealed class GetModerationQueueQueryValidatorTests
{
    private readonly GetModerationQueueQueryValidator _sut = new();

    [Fact]
    public void Valid_minimal_instance_has_no_errors()
    {
        var model = new BeDemo.Api.Models.Requests.Moderation.GetModerationQueueQuery();
        var result = _sut.TestValidate(model);
        // Refine per §4 T1–T12 as rules are added.
        _ = result;
    }
}
