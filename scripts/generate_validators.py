#!/usr/bin/env python3
"""Generate FluentValidation validators + tests for existing request types."""
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / "BeDemo.Api"
VAL = API / "Validation"
TESTS = ROOT / "BeDemo.Api.Tests" / "Validation"

# (validator, type, namespace, folder, rules_snippet_ctor_body)
ENTRIES: list[tuple[str, str, str, str, str]] = [
    ("RegisterResendDtoValidator", "RegisterResendDto", "BeDemo.Api.Models.DTOs", "OAuth",
     "RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(ValidationConstants.EmailMaxLength); RuleFor(x => x.Platform).RegistrationPlatform();"),
    ("RegisterPrefillQueryValidator", "RegisterPrefillQuery", "BeDemo.Api.Models.Requests.OAuth", "OAuth",
     "RuleFor(x => x.Hash).NotEmpty().MaximumLength(ValidationConstants.RegistrationHashMaxLength);"),
    ("LocalizationBundleQueryValidator", "LocalizationBundleQuery", "BeDemo.Api.Models.Requests.OAuth", "OAuth",
     "RuleFor(x => x.V).MaximumLength(64).When(x => !string.IsNullOrEmpty(x.V));"),
    ("UpdateUserRequestValidator", "UpdateUserModel", "BeDemo.Api.Controllers", "Users",
     "RuleFor(x => x.Password).MinimumLength(IdentityPasswordPolicyOptions.RecommendedMinimumLength).When(x => !string.IsNullOrEmpty(x.Password));"),
    ("RegisterPushTokenRequestValidator", "RegisterPushTokenRequestDto", "BeDemo.Api.Models.DTOs", "Users",
     "RuleFor(x => x.RegistrationToken).NotEmpty().MinimumLength(ValidationConstants.PushTokenMinLength).MaximumLength(ValidationConstants.PushTokenMaxLength); RuleFor(x => x.Platform).PushPlatform();"),
    ("UpdateProfileRequestValidator", "UpdateProfileRequest", "BeDemo.Api.Controllers", "Profile",
     "RuleFor(x => x).Must(m => !string.IsNullOrWhiteSpace(m.FirstName) || !string.IsNullOrWhiteSpace(m.LastName)).WithMessage('At least one field required');"),
    ("CreateReelRequestValidator", "CreateReelDto", "BeDemo.Api.Controllers", "Reels",
     "RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength); RuleFor(x => x.VideoUrl).NotEmpty().MaximumLength(ValidationConstants.VideoUrlMaxLength).SafeHttpUrl(); RuleFor(x => x.Description).MaximumLength(ValidationConstants.DescriptionMediumMaxLength);"),
    ("UpdateReelRequestValidator", "UpdateReelDto", "BeDemo.Api.Controllers", "Reels",
     "RuleFor(x => x.Title).MaximumLength(ValidationConstants.TitleMaxLength).When(x => x.Title != null);"),
    ("CreateBlogRequestValidator", "CreateBlogDto", "BeDemo.Api.Controllers", "Blogs",
     "RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength); RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.BlogContentMaxLength);"),
    ("CreateBlogCommentRequestValidator", "CreateBlogCommentDto", "BeDemo.Api.Controllers", "Blogs",
     "RuleFor(x => x.Content).NotEmpty().MaximumLength(ValidationConstants.DescriptionMediumMaxLength);"),
    ("CreateAlbumRequestValidator", "CreateAlbumDto", "BeDemo.Api.Controllers", "Albums",
     "RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);"),
    ("CreateStoryRequestValidator", "CreateStoryDto", "BeDemo.Api.Controllers", "Stories",
     "RuleFor(x => x.Title).NotEmpty().MaximumLength(ValidationConstants.TitleMaxLength);"),
    ("SendFriendRequestRequestValidator", "SendFriendRequestDto", "BeDemo.Api.Controllers", "Social",
     "RuleFor(x => x.ReceiverId).NotEmpty();"),
    ("BlockUserRequestValidator", "BlockUserDto", "BeDemo.Api.Controllers", "Social",
     "RuleFor(x => x.BlockedId).NotEmpty();"),
    ("FollowUserRequestValidator", "FollowUserDto", "BeDemo.Api.Controllers", "Social",
     "RuleFor(x => x.FollowedId).NotEmpty();"),
    ("ModerationDecisionRequestValidator", "ModerationDecisionDto", "BeDemo.Api.Controllers", "Moderation",
     "RuleFor(x => x.Reason).MaximumLength(ValidationConstants.ModerationReasonMaxLength);"),
]

SKIP = {
    "OAuth2TokenRequestValidator", "RegisterRequestValidator", "LoginRequestValidator",
    "RegisterSignupRequestValidator", "RegisterCompleteDtoValidator", "CreateUserRequestValidator",
    "PaginationQueryValidator",
}

VALIDATOR_TMPL = '''using BeDemo.Api.Configuration;
using BeDemo.Api.Validation.Rules;
using FluentValidation;

namespace BeDemo.Api.Validation.{folder};

/// <summary>FluentValidation for <see cref="{ns}.{type}"/> (endpoint-schema-validation §12.1).</summary>
public sealed class {validator} : AbstractValidator<{ns}.{type}>
{{
    public {validator}()
    {{
        {rules}
    }}
}}
'''

TEST_TMPL = '''using FluentValidation.TestHelper;
using {ns};

namespace BeDemo.Api.Tests.Validation.{folder};

public sealed class {validator}Tests
{{
    private readonly {validator} _sut = new();

    [Fact]
    public void Empty_instance_has_validation_errors()
    {{
        var model = new {type}();
        var result = _sut.TestValidate(model);
        result.ShouldHaveValidationErrors();
    }}
}}
'''


def main() -> None:
    n = 0
    for validator, typ, ns, folder, rules in ENTRIES:
        if validator in SKIP:
            continue
        vp = VAL / folder / f"{validator}.cs"
        tp = TESTS / folder / f"{validator}Tests.cs"
        if vp.exists():
            continue
        vp.parent.mkdir(parents=True, exist_ok=True)
        tp.parent.mkdir(parents=True, exist_ok=True)
        vp.write_text(VALIDATOR_TMPL.format(validator=validator, type=typ, ns=ns, folder=folder, rules=rules))
        tp.write_text(TEST_TMPL.format(validator=validator, type=typ, ns=ns, folder=folder))
        n += 2
    print(f"wrote {n} files")


if __name__ == "__main__":
    main()
