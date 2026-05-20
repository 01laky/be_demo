using FluentValidation.TestHelper;
using BeDemo.Api.Validation.Profile;


namespace BeDemo.Api.Tests.Validation.Profile;

public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _sut = new();

    [Fact]
    public void Empty_instance_has_validation_errors()
    {
        var model = new UpdateProfileRequest();
        var result = _sut.TestValidate(model);
        result.ShouldHaveValidationErrors();
    }
}
