namespace BeDemo.Api.Models.Requests.Stories;

public sealed class StoryListQuery
{
    public int FaceId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDir { get; set; }
    /// <summary>When set, narrows published filter; operator inventory lists all states when unset.</summary>
    public bool? IsPublished { get; set; }
}

public sealed class StoryMineQuery
{
    public int? FaceId { get; set; }
}

public sealed class StoryDetailQuery
{
    public int FaceId { get; set; }
}

public sealed class StoryViewQuery
{
    public int FaceId { get; set; }
}

public sealed class StoryScopedQuery
{
    public int FaceId { get; set; }
}
