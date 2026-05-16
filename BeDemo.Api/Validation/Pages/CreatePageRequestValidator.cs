using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreatePageModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreatePageRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreatePageModel>
{
    public CreatePageRequestValidator()
    {
        RuleFor(x => x.FaceId).GreaterThan(0);
        RuleFor(x => x.PageTypeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(ValidationConstants.PagePathMaxLength);
        RuleFor(x => x.Index).GreaterThanOrEqualTo(0);
    }
}
