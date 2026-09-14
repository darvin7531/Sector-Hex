#!/usr/bin/env python3
"""Temporarily sync Monolith-DS ru-RU entity localization into Sector-Hex.

This migration helper does three conservative things:

1. Replaces messages in the legacy lowercase ``_mono`` tree when the same
   Fluent ID exists in Monolith-DS's translated ``_Mono`` tree.
2. Backfills only the fields that the entity audit currently reports missing
   (message value/name and/or ``.desc``) in existing Sector-Hex messages.
3. Stages upstream-translated entity messages that are completely absent from
   Sector-Hex in one deterministic generated FTL file.

It never overwrites an existing Sector-Hex name merely because upstream phrases
it differently, and it never imports unrelated upstream messages. The helper is
intended only for the localization PR migration and can be removed once the
resulting locale files have been reviewed and committed.
"""

from __future__ import annotations

import json
import re
import urllib.parse
import urllib.request
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOCALE_ROOT = ROOT / "Resources" / "Locale" / "ru-RU"
TARGET_ROOT = LOCALE_ROOT / "ss14-ru" / "prototypes" / "_mono"
AUDIT_REPORT = ROOT / "Tools" / "ru_entity_localization_audit.json"
GENERATED_IMPORT = TARGET_ROOT / "_entity_audit_import.ftl"

UPSTREAM_REPO = "Lua-Frontier/Monolith-DS"
UPSTREAM_REF = "master"
UPSTREAM_ROOT = "Resources/Locale/ru-RU/ss14-ru/prototypes/_Mono"
TREE_URL = f"https://api.github.com/repos/{UPSTREAM_REPO}/git/trees/{UPSTREAM_REF}?recursive=1"

MESSAGE_START_RE = re.compile(r"^([A-Za-z0-9][A-Za-z0-9_-]*)\s*=", re.MULTILINE)
MESSAGE_LINE_RE = re.compile(r"^([A-Za-z0-9][A-Za-z0-9_-]*)(\s*=\s*)(.*?)(\r?\n)?$")
ATTRIBUTE_START_RE = re.compile(r"^\s+\.([A-Za-z0-9_-]+)\s*=", re.MULTILINE)


def fetch_text(url: str) -> str:
    request = urllib.request.Request(
        url,
        headers={
            "Accept": "application/vnd.github+json",
            "User-Agent": "Sector-Hex-ru-localization-sync",
        },
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read().decode("utf-8")


def upstream_file_index() -> dict[str, str]:
    payload = json.loads(fetch_text(TREE_URL))
    prefix = UPSTREAM_ROOT + "/"
    result: dict[str, str] = {}

    for entry in payload.get("tree", []):
        path = entry.get("path", "")
        if entry.get("type") != "blob" or not path.startswith(prefix) or not path.endswith(".ftl"):
            continue
        relative = path[len(prefix):]
        result[relative.lower()] = path

    return result


def split_messages(text: str) -> tuple[str, list[tuple[str, str]]]:
    """Return preamble plus ordered ``(message_id, block)`` tuples."""
    matches = list(MESSAGE_START_RE.finditer(text))
    if not matches:
        return text, []

    preamble = text[: matches[0].start()]
    messages: list[tuple[str, str]] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        messages.append((match.group(1), text[match.start():end]))
    return preamble, messages


def message_value(block: str) -> str | None:
    first_line = block.splitlines(keepends=True)[0] if block else ""
    match = MESSAGE_LINE_RE.match(first_line)
    if match is None:
        return None
    return match.group(3).strip()


def attribute_block(block: str, attribute: str) -> str | None:
    """Return one complete Fluent attribute, including continuation lines."""
    lines = block.splitlines(keepends=True)
    start: int | None = None

    for index, line in enumerate(lines[1:], 1):
        match = re.match(r"^\s+\.([A-Za-z0-9_-]+)\s*=", line)
        if match is None:
            continue
        if start is None:
            if match.group(1) == attribute:
                start = index
            continue
        return "".join(lines[start:index])

    if start is None:
        return None

    end = len(lines)
    while end > start and not lines[end - 1].strip():
        end -= 1
    return "".join(lines[start:end])


def has_attribute(block: str, attribute: str) -> bool:
    return any(match.group(1) == attribute for match in ATTRIBUTE_START_RE.finditer(block))


def replace_message_value(local_block: str, upstream_block: str) -> tuple[str, bool]:
    upstream_lines = upstream_block.splitlines(keepends=True)
    local_lines = local_block.splitlines(keepends=True)
    if not upstream_lines or not local_lines:
        return local_block, False

    upstream_match = MESSAGE_LINE_RE.match(upstream_lines[0])
    local_match = MESSAGE_LINE_RE.match(local_lines[0])
    if upstream_match is None or local_match is None:
        return local_block, False

    upstream_value = upstream_match.group(3).strip()
    if not upstream_value:
        return local_block, False

    newline = local_match.group(4) or "\n"
    local_lines[0] = (
        f"{local_match.group(1)}{local_match.group(2)}"
        f"{upstream_match.group(3)}{newline}"
    )
    return "".join(local_lines), True


def append_attribute(local_block: str, upstream_block: str, attribute: str) -> tuple[str, bool]:
    if has_attribute(local_block, attribute):
        return local_block, False

    translated = attribute_block(upstream_block, attribute)
    if translated is None:
        return local_block, False

    lines = local_block.splitlines(keepends=True)
    insert_at = len(lines)
    while insert_at > 0 and not lines[insert_at - 1].strip():
        insert_at -= 1

    if insert_at > 0 and not lines[insert_at - 1].endswith(("\n", "\r")):
        lines[insert_at - 1] += "\n"

    translated = translated.rstrip("\r\n") + "\n"
    lines.insert(insert_at, translated)
    return "".join(lines), True


def merge_translated_messages(target: str, upstream: str) -> tuple[str, int]:
    """Replace matching legacy-English blocks with upstream Russian blocks."""
    target_preamble, target_messages = split_messages(target)
    _, upstream_messages = split_messages(upstream)
    upstream_by_id = {message_id: block for message_id, block in upstream_messages}

    replaced = 0
    output = [target_preamble]
    for message_id, block in target_messages:
        translated = upstream_by_id.get(message_id)
        if translated is not None and translated != block:
            output.append(translated)
            replaced += 1
        else:
            output.append(block)

    return "".join(output), replaced


def load_audit_issues() -> dict[str, dict[str, object]]:
    if not AUDIT_REPORT.is_file():
        raise SystemExit(f"Audit report does not exist: {AUDIT_REPORT}")

    payload = json.loads(AUDIT_REPORT.read_text(encoding="utf-8"))
    return {
        str(issue["key"]): issue
        for issue in payload.get("issues", [])
        if str(issue.get("key", "")).startswith("ent-")
    }


def collect_local_message_ids(exclude: Path | None = None) -> set[str]:
    result: set[str] = set()
    for path in LOCALE_ROOT.rglob("*.ftl"):
        if exclude is not None and path == exclude:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        result.update(MESSAGE_START_RE.findall(text))
    return result


def backfill_existing_messages(
    issues: dict[str, dict[str, object]],
    upstream_by_id: dict[str, str],
) -> tuple[int, int]:
    """Backfill only fields the audit says are missing in existing messages."""
    by_source: dict[Path, list[tuple[str, dict[str, object]]]] = defaultdict(list)

    for key, issue in issues.items():
        source = issue.get("ru_source")
        if not source or key not in upstream_by_id:
            continue
        path = ROOT / str(source)
        if path == GENERATED_IMPORT or not path.is_file():
            continue
        by_source[path].append((key, issue))

    changed_files = 0
    changed_fields = 0

    for path, file_issues in sorted(by_source.items(), key=lambda item: item[0].as_posix()):
        text = path.read_text(encoding="utf-8", errors="replace")
        preamble, messages = split_messages(text)
        issue_by_key = {key: issue for key, issue in file_issues}

        output = [preamble]
        file_fields = 0
        for key, block in messages:
            issue = issue_by_key.get(key)
            upstream_block = upstream_by_id.get(key)
            if issue is None or upstream_block is None:
                output.append(block)
                continue

            updated = block
            missing = {str(value) for value in issue.get("missing", [])}

            if "name" in missing and not (message_value(updated) or ""):
                updated, changed = replace_message_value(updated, upstream_block)
                file_fields += int(changed)

            if "desc" in missing:
                updated, changed = append_attribute(updated, upstream_block, "desc")
                file_fields += int(changed)

            output.append(updated)

        merged = "".join(output)
        if merged != text:
            path.write_text(merged, encoding="utf-8")
            changed_files += 1
            changed_fields += file_fields
            print(
                f"backfilled {path.relative_to(ROOT).as_posix()}: "
                f"{file_fields} missing fields"
            )

    return changed_files, changed_fields


def write_generated_import(
    issues: dict[str, dict[str, object]],
    upstream_by_id: dict[str, str],
) -> int:
    """Stage upstream blocks for audited IDs that are absent from every local FTL file."""
    if GENERATED_IMPORT.exists():
        GENERATED_IMPORT.unlink()

    local_ids = collect_local_message_ids(exclude=GENERATED_IMPORT)
    additions = [
        (key, upstream_by_id[key])
        for key, issue in sorted(issues.items())
        if issue.get("ru_source") is None
        and key not in local_ids
        and key in upstream_by_id
    ]

    if not additions:
        return 0

    header = (
        "# TEMPORARY localization-audit import.\n"
        "# Exact ru-RU entity messages sourced from Lua-Frontier/Monolith-DS.\n"
        "# This file is generated by Tools/sync_ru_mono_translations.py and is\n"
        "# limited to entity IDs that were absent from Sector-Hex's ru-RU locale.\n\n"
    )
    body_parts = [header]
    for _, block in additions:
        body_parts.append(block.rstrip("\r\n") + "\n\n")

    GENERATED_IMPORT.parent.mkdir(parents=True, exist_ok=True)
    GENERATED_IMPORT.write_text("".join(body_parts), encoding="utf-8")
    print(
        f"generated {GENERATED_IMPORT.relative_to(ROOT).as_posix()}: "
        f"{len(additions)} missing messages"
    )
    return len(additions)


def main() -> int:
    if not TARGET_ROOT.is_dir():
        raise SystemExit(f"Target locale tree does not exist: {TARGET_ROOT}")

    issues = load_audit_issues()
    audit_keys = set(issues)
    index = upstream_file_index()

    changed_files = 0
    replaced_messages = 0
    unmatched_files: list[str] = []
    upstream_for_audit: dict[str, str] = {}

    # Fetch every upstream _Mono FTL once. This both refreshes the old lowercase
    # mirror and builds a complete ID -> translated-block index for audit gaps.
    target_by_relative = {
        path.relative_to(TARGET_ROOT).as_posix().lower(): path
        for path in TARGET_ROOT.rglob("*.ftl")
        if path != GENERATED_IMPORT
    }

    for relative_lower, upstream_path in sorted(index.items()):
        encoded_path = urllib.parse.quote(upstream_path, safe="/")
        raw_url = (
            f"https://raw.githubusercontent.com/{UPSTREAM_REPO}/{UPSTREAM_REF}/"
            f"{encoded_path}"
        )
        upstream_text = fetch_text(raw_url)
        _, upstream_messages = split_messages(upstream_text)

        for message_id, block in upstream_messages:
            if message_id in audit_keys:
                upstream_for_audit[message_id] = block

        target_path = target_by_relative.get(relative_lower)
        if target_path is None:
            continue

        target_text = target_path.read_text(encoding="utf-8", errors="replace")
        merged, count = merge_translated_messages(target_text, upstream_text)
        if merged == target_text:
            continue

        target_path.write_text(merged, encoding="utf-8")
        changed_files += 1
        replaced_messages += count
        print(
            f"updated {target_path.relative_to(TARGET_ROOT).as_posix()}: "
            f"{count} message blocks"
        )

    for relative_lower, target_path in sorted(target_by_relative.items()):
        if relative_lower not in index:
            unmatched_files.append(target_path.relative_to(TARGET_ROOT).as_posix())

    backfilled_files, backfilled_fields = backfill_existing_messages(
        issues,
        upstream_for_audit,
    )
    added_messages = write_generated_import(issues, upstream_for_audit)

    print(
        "Monolith ru-RU sync: "
        f"{changed_files} legacy files changed, "
        f"{replaced_messages} legacy message blocks replaced; "
        f"{backfilled_files} locale files backfilled, "
        f"{backfilled_fields} audited fields restored; "
        f"{added_messages} missing messages staged; "
        f"{len(upstream_for_audit)}/{len(audit_keys)} audit IDs found upstream; "
        f"{len(unmatched_files)} legacy files had no upstream counterpart."
    )

    if unmatched_files:
        print("No upstream counterpart:")
        for relative in unmatched_files:
            print(f"  {relative}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
