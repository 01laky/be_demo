using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreateFaceModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateFaceRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreateFaceModel>
{
    public CreateFaceRequestValidator()
    {
        RuleFor(x => x.Index).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(ValidationConstants.DescriptionShortMaxLength);
    }
}
