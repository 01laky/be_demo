namespace BeDemo.Api.Models.Requests.Blogs;

public sealed class BlogListQuery
{
    public int? FaceId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? ApprovalStatus { get; set; }
}
