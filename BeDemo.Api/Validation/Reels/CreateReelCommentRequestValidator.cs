using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreateReelCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateReelCommentRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreateReelCommentDto>
{
    public CreateReelCommentRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
    }
}
