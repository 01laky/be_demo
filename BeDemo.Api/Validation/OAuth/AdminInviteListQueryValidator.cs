using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.OAuth;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.OAuth.AdminInviteListQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class AdminInviteListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.OAuth.AdminInviteListQuery>
{
    public AdminInviteListQueryValidator()
    {
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0); RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}
