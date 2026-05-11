using System.Text.Json;
using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

public static class ContentModerationHelpers
{
    public const int DefaultMaxAttempts = 3;
    public const int DefaultPerContentQueueLimit = 1;
    public const int DefaultBatchSizeLimit = 25;
    public const string AiReviewJobType = "content.ai-review";

    public static string CreatorStatusLabel(ContentApprovalStatus approvalStatus, AiReviewStatus aiReviewStatus) =>
        approvalStatus switch
        {
            ContentApprovalStatus.PendingApproval when aiReviewStatus == AiReviewStatus.InProgress => "Under AI review",
            ContentApprovalStatus.PendingApproval when aiReviewStatus == AiReviewStatus.NeedsHumanReview => "Needs review",
            ContentApprovalStatus.PendingApproval => "Pending approval",
            ContentApprovalStatus.Approved => "Approved",
            ContentApprovalStatus.Rejected => "Rejected",
            ContentApprovalStatus.Removed => "Removed",
            _ => "Pending approval",
        };

    public static bool IsPubliclyVisible(ContentApprovalStatus approvalStatus) =>
        approvalStatus == ContentApprovalStatus.Approved;

    public static bool IsCreatorEditable(ContentApprovalStatus approvalStatus) =>
        approvalStatus is ContentApprovalStatus.PendingApproval or ContentApprovalStatus.Rejected;

    public static bool IsCreatorDeletable(ContentApprovalStatus approvalStatus) =>
        approvalStatus is ContentApprovalStatus.PendingApproval or ContentApprovalStatus.Rejected;

    public static bool IsSafeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    public static AiRecommendationValidationResult ValidateRecommendation(AiReviewRecommendation recommendation)
    {
        if (!Enum.IsDefined(recommendation.Decision))
            return AiRecommendationValidationResult.NeedsHumanReview("Unknown AI decision.");
        if (recommendation.Confidence is < 0 or > 1)
            return AiRecommendationValidationResult.NeedsHumanReview("AI confidence must be between 0 and 1.");
        if (recommendation.RiskLevel == AiReviewRiskLevel.High && recommendation.Decision == AiReviewDecision.Approve)
            return AiRecommendationValidationResult.NeedsHumanReview("High-risk content cannot be auto-approved.");
        if (recommendation.Decision == AiReviewDecision.Reject && string.IsNullOrWhiteSpace(recommendation.Reason))
            return AiRecommendationValidationResult.NeedsHumanReview("Reject recommendations require a reason.");

        return AiRecommendationValidationResult.Valid();
    }

    public static ContentModerationEvent BuildEvent(
        ModeratedContentType contentType,
        int contentId,
        int faceId,
        ContentApprovalStatus? oldApprovalStatus,
        ContentApprovalStatus? newApprovalStatus,
        AiReviewStatus? oldAiReviewStatus,
        AiReviewStatus? newAiReviewStatus,
        ModerationActorType actorType,
        string? actorUserId,
        string? reason,
        string? userMessage,
        string? aiTraceId = null,
        string? aiModelVersion = null) =>
        new()
        {
            ContentType = contentType,
            ContentId = contentId,
            FaceId = faceId,
            OldApprovalStatus = oldApprovalStatus,
            NewApprovalStatus = newApprovalStatus,
            OldAiReviewStatus = oldAiReviewStatus,
            NewAiReviewStatus = newAiReviewStatus,
            ActorType = actorType,
            ActorUserId = actorUserId,
            Reason = RedactForAudit(reason),
            UserMessage = userMessage,
            AiTraceId = aiTraceId,
            AiModelVersion = aiModelVersion,
            CreatedAtUtc = DateTime.UtcNow,
        };

    public static string BuildAiReviewPayload(
        ModeratedContentType contentType,
        int contentId,
        int moderationVersion) =>
        JsonSerializer.Serialize(new
        {
            contentType = contentType.ToString(),
            contentId,
            moderationVersion,
        });

    public static string? RedactForAudit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        return trimmed.Length <= 2000 ? trimmed : string.Concat(trimmed.AsSpan(0, 2000), "...");
    }
}

public sealed record AiReviewRecommendation(
    AiReviewDecision Decision,
    double Confidence,
    AiReviewRiskLevel RiskLevel,
    IReadOnlyList<string> Flags,
    string? Reason,
    string? UserMessage,
    string? ModelVersion,
    string? TraceId);

public sealed record AiRecommendationValidationResult(bool IsValid, string? FallbackReason)
{
    public static AiRecommendationValidationResult Valid() => new(true, null);

    public static AiRecommendationValidationResult NeedsHumanReview(string reason) => new(false, reason);
}
