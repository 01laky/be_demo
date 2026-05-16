using BeDemo.Api.Models.Requests.OAuth;
using BeDemo.Api.Validation.OAuth;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.OAuth;

public sealed class AdminInviteListQueryValidatorTests
{
    private readonly AdminInviteListQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new AdminInviteListQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Take_out_of_range_fails()
    {
        _sut.TestValidate(new AdminInviteListQuery { Take = 0 }).ShouldHaveValidationErrorFor(x => x.Take);
    }
}
