using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Blogs;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Blogs.BlogListQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class BlogListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Blogs.BlogListQuery>
{
    public BlogListQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
    }
}
