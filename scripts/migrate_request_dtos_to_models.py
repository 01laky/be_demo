#!/usr/bin/env python3
"""Move request DTOs from Controllers/*.cs tails into Models/Requests/** and update validator refs."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
API = ROOT / "BeDemo.Api"
CTRL = API / "Controllers"

# type_name -> (target_file_relative_to_Models/Requests, namespace)
MIGRATIONS: dict[str, tuple[str, str]] = {
    "RegisterModel": ("Auth/AuthRequests.cs", "BeDemo.Api.Models.Requests.Auth"),
    "LoginModel": ("Auth/AuthRequests.cs", "BeDemo.Api.Models.Requests.Auth"),
    "OAuth2RegisterModel": ("Auth/AuthRequests.cs", "BeDemo.Api.Models.Requests.Auth"),
    "CreateUserModel": ("Users/UserRequests.cs", "BeDemo.Api.Models.Requests.Users"),
    "UpdateUserModel": ("Users/UserRequests.cs", "BeDemo.Api.Models.Requests.Users"),
    "UpdateProfileRequest": ("Profile/ProfileRequests.cs", "BeDemo.Api.Models.Requests.Profile"),
    "CreateFaceModel": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "UpdateFaceModel": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "SetMyFaceRoleModel": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "FaceProfileCommentDto": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "FaceProfileReviewDto": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "CreateFaceChatRoomDto": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "CreateSystemFaceChatRoomDto": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "UpdateFaceChatRoomDto": ("Faces/FaceRequests.cs", "BeDemo.Api.Models.Requests.Faces"),
    "CreatePageModel": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "UpdatePageModel": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "PageRouteTranslationModel": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "CreatePageTypeModel": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "UpdatePageTypeModel": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "CreatePageComponentDto": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "UpdatePageComponentDto": ("Pages/PageRequests.cs", "BeDemo.Api.Models.Requests.Pages"),
    "CreateReelDto": ("Reels/ReelRequests.cs", "BeDemo.Api.Models.Requests.Reels"),
    "UpdateReelDto": ("Reels/ReelRequests.cs", "BeDemo.Api.Models.Requests.Reels"),
    "CreateReelCommentDto": ("Reels/ReelRequests.cs", "BeDemo.Api.Models.Requests.Reels"),
    "UpdateReelCommentDto": ("Reels/ReelRequests.cs", "BeDemo.Api.Models.Requests.Reels"),
    "CreateBlogDto": ("Blogs/BlogRequests.cs", "BeDemo.Api.Models.Requests.Blogs"),
    "UpdateBlogDto": ("Blogs/BlogRequests.cs", "BeDemo.Api.Models.Requests.Blogs"),
    "CreateBlogCommentDto": ("Blogs/BlogRequests.cs", "BeDemo.Api.Models.Requests.Blogs"),
    "UpdateBlogCommentDto": ("Blogs/BlogRequests.cs", "BeDemo.Api.Models.Requests.Blogs"),
    "CreateAlbumDto": ("Albums/AlbumRequests.cs", "BeDemo.Api.Models.Requests.Albums"),
    "UpdateAlbumDto": ("Albums/AlbumRequests.cs", "BeDemo.Api.Models.Requests.Albums"),
    "CreateAlbumCommentDto": ("Albums/AlbumRequests.cs", "BeDemo.Api.Models.Requests.Albums"),
    "UpdateAlbumCommentDto": ("Albums/AlbumRequests.cs", "BeDemo.Api.Models.Requests.Albums"),
    "CreateStoryDto": ("Stories/StoryRequests.cs", "BeDemo.Api.Models.Requests.Stories"),
    "PublishStoryDto": ("Stories/StoryRequests.cs", "BeDemo.Api.Models.Requests.Stories"),
    "CreateStoryCommentDto": ("Stories/StoryRequests.cs", "BeDemo.Api.Models.Requests.Stories"),
    "SendFriendRequestDto": ("Social/SocialRequests.cs", "BeDemo.Api.Models.Requests.Social"),
    "BlockUserDto": ("Social/SocialRequests.cs", "BeDemo.Api.Models.Requests.Social"),
    "FollowUserDto": ("Social/SocialRequests.cs", "BeDemo.Api.Models.Requests.Social"),
}

STRIP_ATTRS = re.compile(
    r"\[\s*(?:Required|MaxLength|StringLength|Range|EmailAddress|MinLength)[^\]]*\]\s*\n\s*",
    re.MULTILINE,
)


def extract_type(source: str, type_name: str) -> str | None:
    m = re.search(
        rf"(///[^\n]*\n)*(?:public\s+)?(?:sealed\s+)?(?:class|record)\s+{re.escape(type_name)}\b[^{{]*\{{",
        source,
    )
    if not m:
        return None
    start = m.start()
    brace = source.find("{", m.end() - 1)
    depth = 0
    i = brace
    while i < len(source):
        if source[i] == "{":
            depth += 1
        elif source[i] == "}":
            depth -= 1
            if depth == 0:
                return source[start : i + 1]
        i += 1
    return None


def strip_data_annotations(block: str) -> str:
    block = STRIP_ATTRS.sub("", block)
    return block


def main() -> None:
    buckets: dict[str, list[str]] = {}
    removed_from: list[str] = []

    for type_name, (rel_path, ns) in MIGRATIONS.items():
        found = None
        src_file = None
        for path in CTRL.glob("*.cs"):
            text = path.read_text()
            block = extract_type(text, type_name)
            if block:
                found = strip_data_annotations(block)
                src_file = path
                break
        if not found:
            # nested type e.g. WallTicket in FaceWallTicketsController
            for path in CTRL.glob("*.cs"):
                text = path.read_text()
                m = re.search(
                    rf"public\s+sealed\s+class\s+{re.escape(type_name)}\b",
                    text,
                )
                if m:
                    found = strip_data_annotations(extract_type(text, type_name) or "")
                    src_file = path
                    break
        if not found:
            print(f"skip missing {type_name}")
            continue
        buckets.setdefault(rel_path, []).append((type_name, found, ns, src_file))

    for rel_path, items in buckets.items():
        out = API / "Models" / "Requests" / rel_path
        out.parent.mkdir(parents=True, exist_ok=True)
        ns = items[0][2]
        parts = ["namespace " + ns + ";\n\n"]
        usings = set()
        for _, block, _, _ in items:
            if "FaceVisibility" in block:
                usings.add("using BeDemo.Api.Models;\n")
            if "IFormFile" in block:
                usings.add("using Microsoft.AspNetCore.Http;\n")
        parts.extend(sorted(usings))
        if usings:
            parts.append("\n")
        for _, block, _, _ in items:
            parts.append(block.strip() + "\n\n")
        if out.exists():
            existing = out.read_text()
            for _, block, _, _ in items:
                tn = re.search(r"class\s+(\w+)|record\s+(\w+)", block)
                name = (tn.group(1) or tn.group(2)) if tn else ""
                if name and f"class {name}" in existing or f"record {name}" in existing:
                    continue
                existing += "\n" + block.strip() + "\n\n"
            out.write_text(existing)
        else:
            out.write_text("".join(parts))

    # Remove types from controller files (bottom-level only)
    for type_name, (_, ns) in MIGRATIONS.items():
        for path in CTRL.glob("*.cs"):
            text = path.read_text()
            block = extract_type(text, type_name)
            if not block:
                continue
            # only remove if at namespace level (after last controller class close)
            new_text = text.replace(block, "").rstrip() + "\n"
            if new_text != text:
                path.write_text(new_text)
                removed_from.append(f"{path.name}:{type_name}")

    # Update validator and test references
    for type_name, (_, ns) in MIGRATIONS.items():
        short = type_name
        old = f"BeDemo.Api.Controllers.{short}"
        new = f"{ns}.{short}"
        for path in (ROOT / "BeDemo.Api").rglob("*.cs"):
            t = path.read_text()
            if old in t:
                path.write_text(t.replace(old, new))

    for path in CTRL.glob("*.cs"):
        t = path.read_text()
        if "BeDemo.Api.Models.Requests" not in t:
            continue
        # add usings for request namespaces used in file - manual follow-up
    print(f"migrated {len(MIGRATIONS)} types, removed {len(removed_from)} blocks")


if __name__ == "__main__":
    main()
