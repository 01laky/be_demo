using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Moderation;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Controllers.BulkModerationRequest"/> (endpoint-schema-validation §12.1).</summary>
public sealed class BulkModerationRequestValidator : AbstractValidator<BeDemo.Api.Controllers.BulkModerationRequest>
{
    public BulkModerationRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= ValidationConstants.BulkModerationMaxItems);
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.ContentId).GreaterThan(0));
    }
}
