using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Blogs;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.CreateBlogDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class CreateBlogRequestValidator : AbstractValidator<BeDemo.Api.Controllers.CreateBlogDto>
{
    public CreateBlogRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength); RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.BlogContentMaxLength);
    }
}
