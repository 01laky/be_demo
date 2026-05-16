using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Reels;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Reels.ReelListQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ReelListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Reels.ReelListQuery>
{
    public ReelListQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
    }
}
