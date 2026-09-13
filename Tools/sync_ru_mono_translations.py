#!/usr/bin/env python3
"""Temporarily sync translated Monolith Fluent messages into the legacy ru-RU _mono tree.

The Sector-Hex ru-RU locale contains a legacy lowercase ``_mono`` tree whose
files were populated with English source strings.  Lua-Frontier/Monolith-DS has
human Russian translations for the same message IDs under ``_Mono``.

This helper is deliberately conservative:
* it only touches .ftl files that already exist in Sector-Hex's lowercase tree;
* it only replaces message blocks whose exact Fluent ID also exists upstream;
* it never deletes target-only messages or adds unrelated upstream messages;
* path matching is case-insensitive so ``body`` can match upstream ``Body``.

It is intended for the localization PR migration and can be removed once the
legacy tree has been converted and the resulting files are committed.
"""

from __future__ import annotations

import json
import re
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TARGET_ROOT = ROOT / "Resources" / "Locale" / "ru-RU" / "ss14-ru" / "prototypes" / "_mono"
UPSTREAM_REPO = "Lua-Frontier/Monolith-DS"
UPSTREAM_REF = "master"
UPSTREAM_ROOT = "Resources/Locale/ru-RU/ss14-ru/prototypes/_Mono"
TREE_URL = f"https://api.github.com/repos/{UPSTREAM_REPO}/git/trees/{UPSTREAM_REF}?recursive=1"

MESSAGE_START_RE = re.compile(r"^([A-Za-z0-9][A-Za-z0-9_-]*)\s*=", re.MULTILINE)


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
    """Return preamble plus ordered (message_id, block) tuples."""
    matches = list(MESSAGE_START_RE.finditer(text))
    if not matches:
        return text, []

    preamble = text[: matches[0].start()]
    messages: list[tuple[str, str]] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        messages.append((match.group(1), text[match.start():end]))
    return preamble, messages


def merge_translated_messages(target: str, upstream: str) -> tuple[str, int]:
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


def main() -> int:
    if not TARGET_ROOT.is_dir():
        raise SystemExit(f"Target locale tree does not exist: {TARGET_ROOT}")

    index = upstream_file_index()
    changed_files = 0
    replaced_messages = 0
    unmatched_files: list[str] = []

    for target_path in sorted(TARGET_ROOT.rglob("*.ftl")):
        relative = target_path.relative_to(TARGET_ROOT).as_posix()
        upstream_path = index.get(relative.lower())
        if upstream_path is None:
            unmatched_files.append(relative)
            continue

        encoded_path = urllib.parse.quote(upstream_path, safe="/")
        raw_url = f"https://raw.githubusercontent.com/{UPSTREAM_REPO}/{UPSTREAM_REF}/{encoded_path}"
        upstream_text = fetch_text(raw_url)
        target_text = target_path.read_text(encoding="utf-8")
        merged, count = merge_translated_messages(target_text, upstream_text)

        if merged == target_text:
            continue

        target_path.write_text(merged, encoding="utf-8")
        changed_files += 1
        replaced_messages += count
        print(f"updated {relative}: {count} message blocks")

    print(
        f"Monolith ru-RU sync: {changed_files} files changed, "
        f"{replaced_messages} message blocks replaced, "
        f"{len(unmatched_files)} target files had no upstream counterpart."
    )
    if unmatched_files:
        print("No upstream counterpart:")
        for relative in unmatched_files:
            print(f"  {relative}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
