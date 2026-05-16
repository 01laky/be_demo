using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Faces;

/// <summary>FluentValidation for <see cref="BeDemo.Api.Models.Requests.Faces.WallTicketListQuery"/> (endpoint-schema-validation §12.1).</summary>
public sealed class WallTicketListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Faces.WallTicketListQuery>
{
    public WallTicketListQueryValidator()
    {
        this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
    }
}
