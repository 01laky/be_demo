using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.UpdatePageTypeModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class UpdatePageTypeRequestValidator : AbstractValidator<BeDemo.Api.Controllers.UpdatePageTypeModel>
{
    public UpdatePageTypeRequestValidator()
    {
        RuleFor(x => x.Index).MaximumLength(50).When(x => x.Index != null);
    }
}
