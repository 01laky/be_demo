using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Social;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.FollowUserDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class FollowUserRequestValidator : AbstractValidator<BeDemo.Api.Controllers.FollowUserDto>
{
    public FollowUserRequestValidator()
    {
        RuleFor(x => x.FollowedId).NotEmpty();
    }
}
