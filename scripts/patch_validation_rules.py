#!/usr/bin/env python3
"""Replace placeholder NotNull() rules with §11 bounds."""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VAL = ROOT / "BeDemo.Api" / "Validation"

PATCHES: dict[str, str] = {
    "Reels/ReelListQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Reels/ReelDetailQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Reels/ReelByUserQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Reels/ReelCommentCreateQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Reels/CreateReelCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Reels/UpdateReelCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Blogs/BlogListQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Blogs/UpdateBlogRequestValidator.cs": """RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);
        RuleFor(x => x.Content).MaximumLength(ValidationConstants.BlogContentMaxLength).When(x => x.Content != null);""",
    "Blogs/UpdateBlogCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Albums/AlbumListQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Albums/AlbumByUserQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Albums/UpdateAlbumRequestValidator.cs": """RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);""",
    "Albums/CreateAlbumCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Albums/UpdateAlbumCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Stories/StoryListQueryValidator.cs": """RuleFor(x => x.FaceId).PositiveFaceId();""",
    "Stories/StoryMineQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Stories/StoryDetailQueryValidator.cs": """RuleFor(x => x.FaceId).PositiveFaceId();""",
    "Stories/StoryViewQueryValidator.cs": """RuleFor(x => x.FaceId).PositiveFaceId();""",
    "Stories/StoryScopedQueryValidator.cs": """RuleFor(x => x.FaceId).PositiveFaceId();""",
    "Stories/CreateStoryCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Stories/PublishStoryRequestValidator.cs": """RuleFor(x => x.ScheduledPublishAt).Must(d => !d.HasValue || d.Value > DateTime.UtcNow)
            .WithMessage("Scheduled publish must be in the future.").WithErrorCode("val_datetime_future");""",
    "Stories/StoryImageUploadFormValidator.cs": """RuleFor(x => x.File).NotNull().WithErrorCode("val_file_required");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 9);
        RuleFor(x => x.Description).MaximumLength(ValidationConstants.DescriptionMediumMaxLength).When(x => x.Description != null);""",
    "Faces/FaceProfileListQueryValidator.cs": """this.ApplyPaginationRules(x => x.Page, x => x.PageSize);""",
    "Faces/ChatMessagesQueryValidator.cs": """RuleFor(x => x.PageSize).InclusiveBetween(1, ValidationConstants.PageSizeDefaultMax);""",
    "Faces/WallTicketListQueryValidator.cs": """this.ApplyPaginationRules(x => x.Page, x => x.PageSize);""",
    "Faces/WallTicketWriteRequestValidator.cs": """RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(ValidationConstants.WallTicketDescriptionMaxLength);""",
    "Faces/WallTicketCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.WallTicketCommentMaxLength);""",
    "Faces/SetMyFaceRoleRequestValidator.cs": """RuleFor(x => x.UserRoleId).GreaterThan(0);""",
    "Faces/CreateFaceRequestValidator.cs": """RuleFor(x => x.Index).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(ValidationConstants.DescriptionShortMaxLength);""",
    "Faces/UpdateFaceRequestValidator.cs": """RuleFor(x => x.Index).MaximumLength(100).When(x => x.Index != null);
        RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);""",
    "Faces/FaceProfileCommentRequestValidator.cs": """RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);""",
    "Faces/FaceProfileReviewRequestValidator.cs": """RuleFor(x => x.Rating).InclusiveBetween(1, 5);""",
    "Faces/CreateFaceChatRoomRequestValidator.cs": """RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);""",
    "Faces/CreateSystemFaceChatRoomRequestValidator.cs": """RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);""",
    "Faces/UpdateFaceChatRoomRequestValidator.cs": """RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);""",
    "Pages/GetPagesQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Pages/CreatePageRequestValidator.cs": """RuleFor(x => x.FaceId).GreaterThan(0);
        RuleFor(x => x.PageTypeId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);
        RuleFor(x => x.Path).NotEmpty().MaximumLength(ValidationConstants.PagePathMaxLength);
        RuleFor(x => x.Index).GreaterThanOrEqualTo(0);""",
    "Pages/UpdatePageRequestValidator.cs": """RuleFor(x => x.GridSchema).MaximumLength(ValidationConstants.GridSchemaMaxLength).When(x => x.GridSchema != null);""",
    "Pages/UpsertPageTranslationsRequestValidator.cs": """RuleFor(x => x.Translations).NotEmpty().Must(t => t.Count <= ValidationConstants.MaxPageTranslations);
        RuleForEach(x => x.Translations).ChildRules(c => {
            c.RuleFor(t => t.LanguageCode).NotEmpty().MaximumLength(10);
            c.RuleFor(t => t.TranslatedRoute).NotEmpty().MaximumLength(200);
        });""",
    "Pages/CreatePageTypeRequestValidator.cs": """RuleFor(x => x.Index).NotEmpty().MaximumLength(50);""",
    "Pages/UpdatePageTypeRequestValidator.cs": """RuleFor(x => x.Index).MaximumLength(50).When(x => x.Index != null);""",
    "Pages/CreatePageComponentRequestValidator.cs": """RuleFor(x => x.PageId).GreaterThan(0);
        RuleFor(x => x.ComponentTypeId).GreaterThan(0);
        RuleFor(x => x.DisplayModeId).GreaterThan(0);""",
    "Pages/UpdatePageComponentRequestValidator.cs": """RuleFor(x => x.Label).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Label != null);""",
    "Moderation/GetModerationQueueQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();
        RuleFor(x => x.FlagContains).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.FlagContains));
        RuleFor(x => x.MinConfidence).InclusiveBetween(0, 1).When(x => x.MinConfidence.HasValue);
        RuleFor(x => x.MaxConfidence).InclusiveBetween(0, 1).When(x => x.MaxConfidence.HasValue);
        RuleFor(x => x).Must(q => !q.MinConfidence.HasValue || !q.MaxConfidence.HasValue || q.MinConfidence <= q.MaxConfidence)
            .WithErrorCode("val_confidence_range");
        RuleFor(x => x.MinQueueAgeHours).GreaterThanOrEqualTo(0).When(x => x.MinQueueAgeHours.HasValue);""",
    "Moderation/BulkModerationRequestValidator.cs": """RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= ValidationConstants.BulkModerationMaxItems);
        RuleForEach(x => x.Items).ChildRules(i => i.RuleFor(x => x.ContentId).GreaterThan(0));""",
    "Profile/ProfileMeQueryValidator.cs": """RuleFor(x => x.FaceId).OptionalPositiveFaceId();""",
    "Profile/AvatarUploadRequestValidator.cs": """RuleFor(x => x.File).NotNull().WithErrorCode("val_file_required");""",
    "Profile/FaceAvatarUploadRequestValidator.cs": """RuleFor(x => x.File).NotNull().WithErrorCode("val_file_required");
        RuleFor(x => x.FaceId).GreaterThan(0);""",
    "Users/DeletePushTokenQueryValidator.cs": """RuleFor(x => x.InstallationId).MaximumLength(ValidationConstants.InstallationIdMaxLength)
            .When(x => !string.IsNullOrEmpty(x.InstallationId));""",
}

IMPORTS = "using BeDemo.Api.Validation;\nusing BeDemo.Api.Validation.Rules;\n"


def patch_file(rel: str, rules: str) -> None:
    path = VAL / rel
    text = path.read_text()
    if "RuleFor(x => x).NotNull()" not in text:
        return
    if "using BeDemo.Api.Validation.Rules;" not in text:
        text = text.replace("using FluentValidation;\n", f"using FluentValidation;\n{IMPORTS}")
    text = re.sub(
        r"    public \w+Validator\(\)\s*\{\s*//[^\n]*\n\s*RuleFor\(x => x\)\.NotNull\(\);\s*\}",
        f"    public {path.stem}()\n    {{\n        {rules}\n    }}",
        text,
        count=1,
    )
    path.write_text(text)


def main() -> None:
    for rel, rules in PATCHES.items():
        patch_file(rel, rules)
    print(f"patched {len(PATCHES)} validators")


if __name__ == "__main__":
    main()
