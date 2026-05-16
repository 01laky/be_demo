using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.SetMyFaceRoleModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class SetMyFaceRoleRequestValidator : AbstractValidator<BeDemo.Api.Controllers.SetMyFaceRoleModel>
{
    public SetMyFaceRoleRequestValidator()
    {
        RuleFor(x => x.UserRoleId).GreaterThan(0);
    }
}
