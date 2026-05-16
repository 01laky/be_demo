namespace BeDemo.Api.Models.Requests.Stories;

public sealed class StoryListQuery
{
    public int FaceId { get; set; }
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
