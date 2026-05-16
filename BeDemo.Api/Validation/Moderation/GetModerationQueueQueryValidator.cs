using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Moderation;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Moderation.GetModerationQueueQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class GetModerationQueueQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Moderation.GetModerationQueueQuery>
{
    public GetModerationQueueQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
        RuleFor(x => x.FlagContains).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.FlagContains));
        RuleFor(x => x.MinConfidence).InclusiveBetween(0, 1).When(x => x.MinConfidence.HasValue);
        RuleFor(x => x.MaxConfidence).InclusiveBetween(0, 1).When(x => x.MaxConfidence.HasValue);
        RuleFor(x => x).Must(q => !q.MinConfidence.HasValue || !q.MaxConfidence.HasValue || q.MinConfidence <= q.MaxConfidence)
            .WithErrorCode("val_confidence_range");
        RuleFor(x => x.MinQueueAgeHours).GreaterThanOrEqualTo(0).When(x => x.MinQueueAgeHours.HasValue);
    }
}
