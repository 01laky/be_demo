#!/usr/bin/env python3
"""Generate FluentValidation validator + test stubs for endpoint-schema-validation §12.1."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / "BeDemo.Api"
VAL = API / "Validation"
TESTS = ROOT / "BeDemo.Api.Tests" / "Validation"

# validatorName -> (actualType, namespace, folder)
SCHEMAS: list[tuple[str, str, str, str]] = [
    ("RegisterRequestValidator", "RegisterModel", "BeDemo.Api.Controllers", "Auth"),
    ("LoginRequestValidator", "LoginModel", "BeDemo.Api.Controllers", "Auth"),
    ("OAuth2TokenRequestValidator", "OAuth2TokenRequest", "BeDemo.Api.Models.DTOs", "OAuth"),
    ("RegisterSignupRequestValidator", "RegisterRequestDto", "BeDemo.Api.Models.DTOs", "OAuth"),
    ("RegisterResendDtoValidator", "RegisterResendDto", "BeDemo.Api.Models.DTOs", "OAuth"),
    ("RegisterPrefillQueryValidator", "RegisterPrefillQuery", "BeDemo.Api.Models.Requests.OAuth", "OAuth"),
    ("RegisterCompleteDtoValidator", "RegisterCompleteDto", "BeDemo.Api.Models.DTOs", "OAuth"),
    ("LocalizationBundleQueryValidator", "LocalizationBundleQuery", "BeDemo.Api.Models.Requests.OAuth", "OAuth"),
    ("GetUsersQueryValidator", "GetUsersQuery", "BeDemo.Api.Models.Requests.Users", "Users"),
    ("CreateUserRequestValidator", "CreateUserModel", "BeDemo.Api.Controllers", "Users"),
    ("UpdateUserRequestValidator", "UpdateUserModel", "BeDemo.Api.Controllers", "Users"),
    ("RegisterPushTokenRequestValidator", "RegisterPushTokenRequestDto", "BeDemo.Api.Models.DTOs", "Users"),
    ("DeletePushTokenQueryValidator", "DeletePushTokenQuery", "BeDemo.Api.Models.Requests.Users", "Users"),
    ("ProfileMeQueryValidator", "ProfileMeQuery", "BeDemo.Api.Models.Requests.Profile", "Profile"),
    ("UpdateProfileRequestValidator", "UpdateProfileRequest", "BeDemo.Api.Controllers", "Profile"),
    ("AvatarUploadRequestValidator", "AvatarUploadRequest", "BeDemo.Api.Models.Requests.Profile", "Profile"),
    ("FaceAvatarUploadRequestValidator", "FaceAvatarUploadRequest", "BeDemo.Api.Models.Requests.Profile", "Profile"),
    ("SetMyFaceRoleRequestValidator", "SetMyFaceRoleModel", "BeDemo.Api.Controllers", "Faces"),
    ("CreateFaceRequestValidator", "CreateFaceModel", "BeDemo.Api.Controllers", "Faces"),
    ("UpdateFaceRequestValidator", "UpdateFaceModel", "BeDemo.Api.Controllers", "Faces"),
    ("FaceProfileListQueryValidator", "FaceProfileListQuery", "BeDemo.Api.Models.Requests.Faces", "Faces"),
    ("FaceProfileCommentRequestValidator", "FaceProfileCommentDto", "BeDemo.Api.Controllers", "Faces"),
    ("FaceProfileReviewRequestValidator", "FaceProfileReviewDto", "BeDemo.Api.Controllers", "Faces"),
    ("CreateFaceChatRoomRequestValidator", "CreateFaceChatRoomDto", "BeDemo.Api.Controllers", "Faces"),
    ("CreateSystemFaceChatRoomRequestValidator", "CreateSystemFaceChatRoomDto", "BeDemo.Api.Controllers", "Faces"),
    ("UpdateFaceChatRoomRequestValidator", "UpdateFaceChatRoomDto", "BeDemo.Api.Controllers", "Faces"),
    ("ChatMessagesQueryValidator", "ChatMessagesQuery", "BeDemo.Api.Models.Requests.Faces", "Faces"),
    ("WallTicketListQueryValidator", "WallTicketListQuery", "BeDemo.Api.Models.Requests.Faces", "Faces"),
    ("WallTicketWriteRequestValidator", "WallTicketWriteDto", "BeDemo.Api.Controllers", "Faces"),
    ("WallTicketCommentRequestValidator", "WallTicketCommentDto", "BeDemo.Api.Controllers", "Faces"),
    ("GetPagesQueryValidator", "GetPagesQuery", "BeDemo.Api.Models.Requests.Pages", "Pages"),
    ("CreatePageRequestValidator", "CreatePageModel", "BeDemo.Api.Controllers", "Pages"),
    ("UpdatePageRequestValidator", "UpdatePageModel", "BeDemo.Api.Controllers", "Pages"),
    ("UpsertPageTranslationsRequestValidator", "UpsertPageTranslationsRequest", "BeDemo.Api.Models.Requests.Pages", "Pages"),
    ("CreatePageTypeRequestValidator", "CreatePageTypeModel", "BeDemo.Api.Controllers", "Pages"),
    ("UpdatePageTypeRequestValidator", "UpdatePageTypeModel", "BeDemo.Api.Controllers", "Pages"),
    ("CreatePageComponentRequestValidator", "CreatePageComponentDto", "BeDemo.Api.Controllers", "Pages"),
    ("UpdatePageComponentRequestValidator", "UpdatePageComponentDto", "BeDemo.Api.Controllers", "Pages"),
    ("GetModerationQueueQueryValidator", "GetModerationQueueQuery", "BeDemo.Api.Models.Requests.Moderation", "Moderation"),
    ("BulkModerationRequestValidator", "BulkModerationRequest", "BeDemo.Api.Models.Requests.Moderation", "Moderation"),
    ("ModerationDecisionRequestValidator", "ModerationDecisionDto", "BeDemo.Api.Controllers", "Moderation"),
    ("StatsTimeseriesQueryValidator", "StatsTimeseriesQuery", "BeDemo.Api.Models.Requests.Stats", "Stats"),
    ("ReelListQueryValidator", "ReelListQuery", "BeDemo.Api.Models.Requests.Reels", "Reels"),
    ("ReelDetailQueryValidator", "ReelDetailQuery", "BeDemo.Api.Models.Requests.Reels", "Reels"),
    ("ReelByUserQueryValidator", "ReelByUserQuery", "BeDemo.Api.Models.Requests.Reels", "Reels"),
    ("CreateReelRequestValidator", "CreateReelDto", "BeDemo.Api.Controllers", "Reels"),
    ("UpdateReelRequestValidator", "UpdateReelDto", "BeDemo.Api.Controllers", "Reels"),
    ("CreateReelCommentRequestValidator", "CreateReelCommentDto", "BeDemo.Api.Controllers", "Reels"),
    ("ReelCommentCreateQueryValidator", "ReelCommentCreateQuery", "BeDemo.Api.Models.Requests.Reels", "Reels"),
    ("UpdateReelCommentRequestValidator", "UpdateReelCommentDto", "BeDemo.Api.Controllers", "Reels"),
    ("BlogListQueryValidator", "BlogListQuery", "BeDemo.Api.Models.Requests.Blogs", "Blogs"),
    ("CreateBlogRequestValidator", "CreateBlogDto", "BeDemo.Api.Controllers", "Blogs"),
    ("UpdateBlogRequestValidator", "UpdateBlogDto", "BeDemo.Api.Controllers", "Blogs"),
    ("CreateBlogCommentRequestValidator", "CreateBlogCommentDto", "BeDemo.Api.Controllers", "Blogs"),
    ("UpdateBlogCommentRequestValidator", "UpdateBlogCommentDto", "BeDemo.Api.Controllers", "Blogs"),
    ("AlbumListQueryValidator", "AlbumListQuery", "BeDemo.Api.Models.Requests.Albums", "Albums"),
    ("AlbumByUserQueryValidator", "AlbumByUserQuery", "BeDemo.Api.Models.Requests.Albums", "Albums"),
    ("CreateAlbumRequestValidator", "CreateAlbumDto", "BeDemo.Api.Controllers", "Albums"),
    ("UpdateAlbumRequestValidator", "UpdateAlbumDto", "BeDemo.Api.Controllers", "Albums"),
    ("CreateAlbumCommentRequestValidator", "CreateAlbumCommentDto", "BeDemo.Api.Controllers", "Albums"),
    ("UpdateAlbumCommentRequestValidator", "UpdateAlbumCommentDto", "BeDemo.Api.Controllers", "Albums"),
    ("StoryListQueryValidator", "StoryListQuery", "BeDemo.Api.Models.Requests.Stories", "Stories"),
    ("StoryMineQueryValidator", "StoryMineQuery", "BeDemo.Api.Models.Requests.Stories", "Stories"),
    ("StoryDetailQueryValidator", "StoryDetailQuery", "BeDemo.Api.Models.Requests.Stories", "Stories"),
    ("CreateStoryRequestValidator", "CreateStoryDto", "BeDemo.Api.Controllers", "Stories"),
    ("PublishStoryRequestValidator", "PublishStoryDto", "BeDemo.Api.Controllers", "Stories"),
    ("StoryViewQueryValidator", "StoryViewQuery", "BeDemo.Api.Models.Requests.Stories", "Stories"),
    ("StoryImageUploadFormValidator", "StoryImageUploadForm", "BeDemo.Api.Models.Requests.Stories", "Stories"),
    ("CreateStoryCommentRequestValidator", "CreateStoryCommentDto", "BeDemo.Api.Controllers", "Stories"),
    ("StoryScopedQueryValidator", "StoryScopedQuery", "BeDemo.Api.Models.Requests.Stories", "Stories"),
    ("SendFriendRequestRequestValidator", "SendFriendRequestDto", "BeDemo.Api.Controllers", "Social"),
    ("BlockUserRequestValidator", "BlockUserDto", "BeDemo.Api.Controllers", "Social"),
    ("FollowUserRequestValidator", "FollowUserDto", "BeDemo.Api.Controllers", "Social"),
    ("MessageHistoryQueryValidator", "MessageHistoryQuery", "BeDemo.Api.Models.Requests.Social", "Social"),
    ("NotificationsListQueryValidator", "NotificationsListQuery", "BeDemo.Api.Models.Requests.Social", "Social"),
]

SKIP = {"OAuth2TokenRequestValidator"}  # hand-written

VALIDATOR_BODY = '''using FluentValidation;

namespace BeDemo.Api.Validation.{folder};

/// <summary>FluentValidation for <see cref="{ns}.{type}"/> (endpoint-schema-validation §12.1).</summary>
public sealed class {validator} : AbstractValidator<{ns}.{type}>
{{
    public {validator}()
    {{
        // Bounds: see docs/prompts/endpoint-schema-validation-agent-prompt.md §11 + §18.
        RuleFor(x => x).NotNull();
    }}
}}
'''

TEST_BODY = '''using FluentValidation.TestHelper;

namespace BeDemo.Api.Tests.Validation.{folder};

public sealed class {validator}Tests
{{
    private readonly {validator} _sut = new();

    [Fact]
    public void Valid_minimal_instance_has_no_errors()
    {{
        var model = new {ns}.{type}();
        var result = _sut.TestValidate(model);
        // Refine per §4 T1–T12 as rules are added.
        _ = result;
    }}
}}
'''


def main() -> None:
    created = 0
    for validator, typ, ns, folder in SCHEMAS:
        if validator in SKIP:
            continue
        vpath = VAL / folder / f"{validator}.cs"
        tpath = TESTS / folder / f"{validator}Tests.cs"
        if vpath.exists():
            continue
        vpath.parent.mkdir(parents=True, exist_ok=True)
        tpath.parent.mkdir(parents=True, exist_ok=True)
        vpath.write_text(VALIDATOR_BODY.format(validator=validator, type=typ, ns=ns, folder=folder))
        tpath.write_text(TEST_BODY.format(validator=validator, type=typ, ns=ns, folder=folder))
        created += 2
    print(f"created {created} files")


if __name__ == "__main__":
    main()
