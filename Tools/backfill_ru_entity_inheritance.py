#!/usr/bin/env python3
"""Backfill audited entity localization from already-translated parent fields.

RobustToolbox localizes an entity by the concrete prototype ID, even while it
walks YAML parents for fallback SetName/SetDesc values. That means a child can
fall back to an English YAML parent field unless the child has its own ent-ID.

For audit gaps whose effective YAML value comes from another prototype, this
script reuses an existing translated parent Fluent value with references such
as ``{ ent-BaseBullet.desc }``. It only emits references when the referenced
pattern is demonstrably Russian (contains Cyrillic, or resolves through pure
Fluent entity references to a Russian pattern). It never invents translations
for child-owned YAML strings.
"""

from __future__ import annotations

import json
import re
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOCALE_ROOT = ROOT / "Resources" / "Locale" / "ru-RU"
AUDIT_REPORT = ROOT / "Tools" / "ru_entity_localization_audit.json"
GENERATED_FILE = LOCALE_ROOT / "_Hex" / "generated" / "entity-inherited-overrides.ftl"

MESSAGE_RE = re.compile(r"^([A-Za-z0-9][A-Za-z0-9_-]*)\s*=", re.MULTILINE)
FIRST_LINE_RE = re.compile(r"^([A-Za-z0-9][A-Za-z0-9_-]*)(\s*=\s*)(.*?)(\r?\n)?$")
ATTR_RE = re.compile(r"^\s+\.([A-Za-z0-9_-]+)\s*=\s*(.*)$")
ENTITY_REF_RE = re.compile(r"\{\s*(ent-[A-Za-z0-9_-]+)(?:\.([A-Za-z0-9_-]+))?\s*\}")
CYRILLIC_RE = re.compile(r"[А-Яа-яЁё]")
ASCII_WORD_RE = re.compile(r"[A-Za-z]")


@dataclass(frozen=True)
class MessageDef:
    path: Path
    block: str


def split_messages(text: str) -> tuple[str, list[tuple[str, str]]]:
    matches = list(MESSAGE_RE.finditer(text))
    if not matches:
        return text, []

    preamble = text[: matches[0].start()]
    result: list[tuple[str, str]] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        result.append((match.group(1), text[match.start():end]))
    return preamble, result


def message_value(block: str) -> str | None:
    if not block:
        return None
    first = block.splitlines()[0]
    match = FIRST_LINE_RE.match(first)
    if match is None:
        return None
    value = match.group(3).strip()
    return value or None


def message_attribute(block: str, attribute: str) -> str | None:
    lines = block.splitlines()
    for index, line in enumerate(lines[1:], 1):
        match = ATTR_RE.match(line)
        if match is None or match.group(1) != attribute:
            continue

        chunks = [match.group(2).strip()]
        cursor = index + 1
        while cursor < len(lines):
            following = lines[cursor]
            if ATTR_RE.match(following):
                break
            if following and not following[0].isspace():
                break
            if following.strip():
                chunks.append(following.strip())
            cursor += 1
        value = " ".join(chunk for chunk in chunks if chunk)
        return value or None
    return None


def load_definitions(exclude: Path | None = None) -> dict[str, list[MessageDef]]:
    definitions: dict[str, list[MessageDef]] = defaultdict(list)
    for path in sorted(LOCALE_ROOT.rglob("*.ftl")):
        if exclude is not None and path == exclude:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        _, messages = split_messages(text)
        for key, block in messages:
            definitions[key].append(MessageDef(path=path, block=block))
    return definitions


def pattern_for(definition: MessageDef, attribute: str | None) -> str | None:
    if attribute is None:
        return message_value(definition.block)
    return message_attribute(definition.block, attribute)


def translated_pattern(
    key: str,
    attribute: str | None,
    definitions: dict[str, list[MessageDef]],
    seen: set[tuple[str, str | None]] | None = None,
) -> str | None:
    """Return a safely reusable translated pattern for ``key`` if one exists."""
    marker = (key, attribute)
    if seen is None:
        seen = set()
    if marker in seen:
        return None
    next_seen = set(seen)
    next_seen.add(marker)

    for definition in definitions.get(key, []):
        pattern = pattern_for(definition, attribute)
        if not pattern:
            continue

        if CYRILLIC_RE.search(pattern):
            return pattern

        refs = list(ENTITY_REF_RE.finditer(pattern))
        if not refs:
            continue

        # Be conservative: a pure reference chain is safe, but an English word
        # surrounding a reference is not considered translated automatically.
        remainder = ENTITY_REF_RE.sub("", pattern)
        if ASCII_WORD_RE.search(remainder):
            continue

        safe = True
        for ref in refs:
            ref_key = ref.group(1)
            ref_attribute = ref.group(2)
            if translated_pattern(ref_key, ref_attribute, definitions, next_seen) is None:
                safe = False
                break
        if safe:
            return pattern

    return None


def append_attribute(block: str, attribute: str, pattern: str) -> tuple[str, bool]:
    if message_attribute(block, attribute) is not None:
        return block, False

    lines = block.splitlines(keepends=True)
    insert_at = len(lines)
    while insert_at > 0 and not lines[insert_at - 1].strip():
        insert_at -= 1

    if insert_at > 0 and not lines[insert_at - 1].endswith(("\n", "\r")):
        lines[insert_at - 1] += "\n"
    lines.insert(insert_at, f"    .{attribute} = {pattern}\n")
    return "".join(lines), True


def replace_empty_value(block: str, pattern: str) -> tuple[str, bool]:
    lines = block.splitlines(keepends=True)
    if not lines:
        return block, False
    match = FIRST_LINE_RE.match(lines[0])
    if match is None or match.group(3).strip():
        return block, False

    newline = match.group(4) or "\n"
    lines[0] = f"{match.group(1)}{match.group(2)}{pattern}{newline}"
    return "".join(lines), True


def load_issues() -> list[dict[str, object]]:
    payload = json.loads(AUDIT_REPORT.read_text(encoding="utf-8"))
    return list(payload.get("issues", []))


def main() -> int:
    if not AUDIT_REPORT.is_file():
        raise SystemExit(f"Missing audit report: {AUDIT_REPORT}")

    if GENERATED_FILE.exists():
        GENERATED_FILE.unlink()

    definitions = load_definitions(exclude=GENERATED_FILE)
    issues = load_issues()

    patches_by_path: dict[Path, dict[str, tuple[str | None, str | None]]] = defaultdict(dict)
    generated: dict[str, tuple[str | None, str | None]] = {}

    for issue in issues:
        key = str(issue["key"])
        prototype_id = str(issue["id"])
        missing = {str(value) for value in issue.get("missing", [])}

        name_ref: str | None = None
        desc_ref: str | None = None

        name_source = issue.get("name_source")
        if "name" in missing and name_source and str(name_source) != prototype_id:
            source_key = f"ent-{name_source}"
            if translated_pattern(source_key, None, definitions) is not None:
                name_ref = f"{{ {source_key} }}"

        desc_source = issue.get("description_source")
        if "desc" in missing and desc_source and str(desc_source) != prototype_id:
            source_key = f"ent-{desc_source}"
            if translated_pattern(source_key, "desc", definitions) is not None:
                desc_ref = f"{{ {source_key}.desc }}"

        if name_ref is None and desc_ref is None:
            continue

        ru_source = issue.get("ru_source")
        if ru_source:
            patches_by_path[ROOT / str(ru_source)][key] = (name_ref, desc_ref)
        else:
            generated[key] = (name_ref, desc_ref)

    changed_files = 0
    filled_fields = 0

    for path, patches in sorted(patches_by_path.items(), key=lambda item: item[0].as_posix()):
        if not path.is_file():
            continue
        original = path.read_text(encoding="utf-8", errors="replace")
        preamble, messages = split_messages(original)
        output = [preamble]
        file_fields = 0

        for key, block in messages:
            refs = patches.get(key)
            if refs is None:
                output.append(block)
                continue

            name_ref, desc_ref = refs
            updated = block
            if name_ref is not None:
                updated, changed = replace_empty_value(updated, name_ref)
                file_fields += int(changed)
            if desc_ref is not None:
                updated, changed = append_attribute(updated, "desc", desc_ref)
                file_fields += int(changed)
            output.append(updated)

        merged = "".join(output)
        if merged != original:
            path.write_text(merged, encoding="utf-8")
            changed_files += 1
            filled_fields += file_fields
            print(f"updated {path.relative_to(ROOT)}: {file_fields} inherited fields")

    if generated:
        GENERATED_FILE.parent.mkdir(parents=True, exist_ok=True)
        chunks = [
            "# Generated localization overrides for entity fields inherited from translated parents.\n",
            "# These are required because RobustToolbox resolves entity localization by concrete prototype ID.\n\n",
        ]
        for key, (name_ref, desc_ref) in sorted(generated.items()):
            chunks.append(f"{key} = {name_ref or ''}\n")
            if desc_ref is not None:
                chunks.append(f"    .desc = {desc_ref}\n")
            chunks.append("\n")
            filled_fields += int(name_ref is not None) + int(desc_ref is not None)
        GENERATED_FILE.write_text("".join(chunks), encoding="utf-8")
        changed_files += 1
        print(f"generated {GENERATED_FILE.relative_to(ROOT)}: {len(generated)} entity messages")

    print(
        f"Inherited ru-RU backfill: {changed_files} files changed, "
        f"{filled_fields} missing fields filled from translated parents."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
