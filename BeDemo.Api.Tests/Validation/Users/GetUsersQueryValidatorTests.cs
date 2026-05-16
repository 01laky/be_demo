using BeDemo.Api.Models.Requests.Users;
using BeDemo.Api.Validation.Users;
using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.Users;

public sealed class GetUsersQueryValidatorTests
{
    private readonly GetUsersQueryValidator _sut = new();

    [Fact]
    public void Defaults_are_valid()
    {
        _sut.TestValidate(new GetUsersQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PageSize_over_max_fails()
    {
        _sut.TestValidate(new GetUsersQuery { PageSize = 101 }).ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}
