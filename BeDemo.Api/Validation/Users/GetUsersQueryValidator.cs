using BeDemo.Api.Models.Requests.Users;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.Users;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator() => this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
}
