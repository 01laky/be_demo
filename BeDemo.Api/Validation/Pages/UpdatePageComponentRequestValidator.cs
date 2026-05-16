using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.UpdatePageComponentDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdatePageComponentRequestValidator : AbstractValidator<BeDemo.Api.Controllers.UpdatePageComponentDto>
{
    public UpdatePageComponentRequestValidator()
    {
        RuleFor(x => x.Label).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Label != null);
    }
}
