using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Users;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.UpdateUserModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateUserRequestValidator : AbstractValidator<BeDemo.Api.Controllers.UpdateUserModel>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Password).MinimumLength(IdentityPasswordPolicyOptions.RecommendedMinimumLength).When(x => !string.IsNullOrEmpty(x.Password));
    }
}
