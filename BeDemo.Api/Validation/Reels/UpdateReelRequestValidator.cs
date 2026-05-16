using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.UpdateReelDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateReelRequestValidator : AbstractValidator<BeDemo.Api.Controllers.UpdateReelDto>
{
    public UpdateReelRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);
    }
}
