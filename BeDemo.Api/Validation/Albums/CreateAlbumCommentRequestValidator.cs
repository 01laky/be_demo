using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Albums;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreateAlbumCommentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateAlbumCommentRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreateAlbumCommentDto>
{
    public CreateAlbumCommentRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);
    }
}
