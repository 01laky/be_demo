using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreatePageTypeModel"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreatePageTypeRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreatePageTypeModel>
{
    public CreatePageTypeRequestValidator()
    {
        RuleFor(x => x.Index).NotEmpty().MaximumLength(50);
    }
}
