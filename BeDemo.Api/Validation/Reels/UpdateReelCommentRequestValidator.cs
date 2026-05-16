using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.UpdateReelCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdateReelCommentRequestValidator : AbstractValidator<BeDemo.Api.Controllers.UpdateReelCommentDto>
{
    public UpdateReelCommentRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
    }
}
