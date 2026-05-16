using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Moderation;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.ModerationDecisionDto"/> (endpoint-schema-validation §12.1).</summary>
public sealed class ModerationDecisionRequestValidator : AbstractValidator<BeDemo.Api.Controllers.ModerationDecisionDto>
{
    public ModerationDecisionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(ValidationConstants.ModerationReasonMaxLength);
    }
}
