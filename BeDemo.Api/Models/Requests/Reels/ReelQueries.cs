namespace BeDemo.Api.Models.Requests.Reels;

public sealed class ReelListQuery
{
    public int? FaceId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    public string? ApprovalStatus { get; set; }
}

public sealed class ReelDetailQuery
{
    public int? FaceId { get; set; }
}

public sealed class ReelByUserQuery
{
    public int? FaceId { get; set; }
}

public sealed class ReelCommentCreateQuery
{
    public int? FaceId { get; set; }
}
