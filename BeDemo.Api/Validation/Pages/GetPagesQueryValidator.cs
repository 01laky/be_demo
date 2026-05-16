using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Pages;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Pages.GetPagesQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class GetPagesQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Pages.GetPagesQuery>
{
    public GetPagesQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
    }
}
