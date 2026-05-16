using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreateReelDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateReelRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreateReelDto>
{
    public CreateReelRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength); RuleFor(x => x.VideoUrl).NotEmpty().MaximumLength(ValidationConstants.VideoUrlMaxLength).SafeHttpUrl(); RuleFor(x => x.Description).MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
    }
}
