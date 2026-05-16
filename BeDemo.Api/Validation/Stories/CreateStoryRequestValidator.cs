using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreateStoryDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateStoryRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreateStoryDto>
{
    public CreateStoryRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
    }
}
