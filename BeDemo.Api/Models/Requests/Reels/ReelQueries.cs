namespace BeDemo.Api.Models.Requests.Reels;

public sealed class ReelListQuery
{
    public int? FaceId { get; set; }
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
