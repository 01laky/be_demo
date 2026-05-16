using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Profile;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Profile.UpdateProfileRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<BeDemo.Api.Models.Requests.Profile.UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x).Must(m => !string.IsNullOrWhiteSpace(m.FirstName) || !string.IsNullOrWhiteSpace(m.LastName))
            .WithMessage("At least one field is required.");
    }
}
