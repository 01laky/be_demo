using FluentValidation;
using BeDemo.Api.Validation;
using BeDemo.Api.Validation.Rules;

namespace BeDemo.Api.Validation.Blogs;

public sealed class BlogListQueryValidator : AbstractValidator<BeDemo.Api.Models.Requests.Blogs.BlogListQuery>
{
    private static readonly string[] SortWhitelist = ["id", "title", "createdAt", "updatedAt", "approvalStatus"];

    public BlogListQueryValidator()
    {
        RuleFor(x => x.FaceId).OptionalPositiveFaceId();
        this.ApplyPaginationRules(x => x.Page, x => x.PageSize);
        this.ApplyListSortRules(x => x.SortBy, x => x.SortDir, SortWhitelist);
        RuleFor(x => x.Search).MaximumLength(200).NoNullBytes().When(x => !string.IsNullOrEmpty(x.Search));
        RuleFor(x => x.ApprovalStatus)
            .Must(s => string.IsNullOrEmpty(s) || Enum.TryParse<BeDemo.Api.Models.ContentApprovalStatus>(s, true, out _))
            .WithMessage("Invalid approvalStatus")
            .When(x => !string.IsNullOrEmpty(x.ApprovalStatus));
    }
}
