using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Stories;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Stories.StoryListQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class StoryListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Stories.StoryListQuery>
{
    public StoryListQueryValidator()
    {
        RuleFor(x => x.FaceId).PositiveFaceId();
    }
}
