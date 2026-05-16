using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.DTOs.AdminCreateRegistrationInviteDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class AdminCreateRegistrationInviteValidator : AbstractValidator<BeDemo.Api.Models.DTOs.AdminCreateRegistrationInviteDto>
{
    public AdminCreateRegistrationInviteValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength);
    }
}
