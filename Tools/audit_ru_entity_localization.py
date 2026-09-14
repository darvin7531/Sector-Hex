#!/usr/bin/env python3
"""Audit ru-RU localization coverage for entity prototypes.

Unlike validate_ru_localization.py, this checks prototype-facing localization:
* concrete entity prototypes with an effective name need an ``ent-<id>`` value;
* concrete entity prototypes with an effective description need ``.desc``;
* inherited YAML name/description values are followed through entity parents.

The parser intentionally only reads top-level entity prototype fields. This keeps
it dependency-free and avoids confusing component ``name``/``description`` fields
with the entity's player-visible metadata.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parent.parent
PROTOTYPE_ROOT = ROOT / "Resources" / "Prototypes"
LOCALE_ROOT = ROOT / "Resources" / "Locale" / "ru-RU"

ENTRY_RE = re.compile(r"^- type:\s*([^#\s]+)")
FIELD_RE = re.compile(r"^  ([A-Za-z][A-Za-z0-9_-]*):(?:\s*(.*?))?\s*$")
LIST_ITEM_RE = re.compile(r"^  -\s+(.+?)\s*$")
FTL_MESSAGE_RE = re.compile(r"^(ent-[A-Za-z0-9_-]+)\s*=\s*(.*)$")
FTL_ATTR_RE = re.compile(r"^\s+\.([A-Za-z0-9_-]+)\s*=")
ANCHOR_RE = re.compile(r"&([A-Za-z0-9_-]+)\s+([^\s#,\]\}]+)")


@dataclass(frozen=True)
class EntityPrototype:
    prototype_id: str
    parents: tuple[str, ...]
    name: str | None
    description: str | None
    abstract: bool
    source: str
    line: int


@dataclass
class FluentMessage:
    value: str
    attributes: set[str]
    source: str


def strip_yaml_comment(value: str) -> str:
    """Remove a YAML inline comment while preserving # inside quoted scalars."""
    quote: str | None = None
    escaped = False

    for index, char in enumerate(value):
        if escaped:
            escaped = False
            continue
        if char == "\\" and quote == '"':
            escaped = True
            continue
        if quote is not None:
            if char == quote:
                quote = None
            continue
        if char in {'"', "'"}:
            quote = char
            continue
        if char == "#" and (index == 0 or value[index - 1].isspace()):
            return value[:index].rstrip()

    return value.rstrip()


def unquote(value: str | None) -> str | None:
    if value is None:
        return None
    value = strip_yaml_comment(value.strip())
    if not value or value in {"null", "~"}:
        return None
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {'"', "'"}:
        return value[1:-1]
    return value


def collect_scalar_anchors(text: str) -> dict[str, str]:
    """Collect the simple scalar anchors used by entity prototype IDs.

    SS14 prototype YAML occasionally assigns an entity ID to an alias whose anchor
    is declared inside a component field, e.g. ``boardPrototype: &board Foo`` and
    later ``id: *board``. A regex-only prototype reader must resolve those aliases
    or it reports impossible localization IDs such as ``ent-*board``.
    """
    anchors: dict[str, str] = {}
    for line in text.splitlines():
        for match in ANCHOR_RE.finditer(strip_yaml_comment(line)):
            name, value = match.groups()
            parsed = unquote(value)
            if parsed:
                anchors[name] = parsed
    return anchors


def resolve_alias(value: str | None, anchors: dict[str, str]) -> str | None:
    parsed = unquote(value)
    if parsed is None:
        return None
    if parsed.startswith("*"):
        return anchors.get(parsed[1:], parsed)
    return parsed


def iter_top_level_entries(text: str) -> Iterable[tuple[int, list[str]]]:
    lines = text.splitlines()
    starts = [index for index, line in enumerate(lines) if ENTRY_RE.match(line)]
    for pos, start in enumerate(starts):
        end = starts[pos + 1] if pos + 1 < len(starts) else len(lines)
        yield start + 1, lines[start:end]


def parse_entity_block(
    lines: list[str],
    source: str,
    line_number: int,
    anchors: dict[str, str],
) -> EntityPrototype | None:
    first = ENTRY_RE.match(lines[0])
    if first is None or first.group(1) != "entity":
        return None

    fields: dict[str, str | None] = {}
    parents: list[str] = []
    reading_parent_list = False

    for line in lines[1:]:
        field = FIELD_RE.match(line)
        if field:
            key, raw_value = field.groups()
            value = strip_yaml_comment(raw_value.strip()) if raw_value is not None else ""
            fields[key] = value
            reading_parent_list = key == "parent" and not value
            if key == "parent" and value:
                parsed = resolve_alias(value, anchors)
                if parsed:
                    # Inline YAML lists are uncommon here, but supporting them is cheap.
                    if parsed.startswith("[") and parsed.endswith("]"):
                        parents.extend(
                            resolved
                            for part in parsed[1:-1].split(",")
                            if (resolved := resolve_alias(part.strip(), anchors))
                        )
                    else:
                        parents.append(parsed)
            continue

        if reading_parent_list:
            item = LIST_ITEM_RE.match(line)
            if item:
                parsed = resolve_alias(item.group(1), anchors)
                if parsed:
                    parents.append(parsed)
                continue
            if line and not line.startswith("    "):
                reading_parent_list = False

    prototype_id = resolve_alias(fields.get("id"), anchors)
    if not prototype_id:
        return None

    abstract = (resolve_alias(fields.get("abstract"), anchors) or "").lower() == "true"
    return EntityPrototype(
        prototype_id=prototype_id,
        parents=tuple(parents),
        name=resolve_alias(fields.get("name"), anchors),
        description=resolve_alias(fields.get("description"), anchors),
        abstract=abstract,
        source=source,
        line=line_number,
    )


def load_entities() -> dict[str, EntityPrototype]:
    entities: dict[str, EntityPrototype] = {}
    for file in PROTOTYPE_ROOT.rglob("*.yml"):
        source = file.relative_to(ROOT).as_posix()
        text = file.read_text(encoding="utf-8", errors="replace")
        anchors = collect_scalar_anchors(text)
        for line_number, block in iter_top_level_entries(text):
            entity = parse_entity_block(block, source, line_number, anchors)
            if entity is not None:
                entities[entity.prototype_id] = entity
    return entities


def load_fluent_messages() -> dict[str, FluentMessage]:
    messages: dict[str, FluentMessage] = {}
    for file in LOCALE_ROOT.rglob("*.ftl"):
        source = file.relative_to(ROOT).as_posix()
        current: FluentMessage | None = None
        for line in file.read_text(encoding="utf-8", errors="replace").splitlines():
            message_match = FTL_MESSAGE_RE.match(line)
            if message_match:
                key, value = message_match.groups()
                current = FluentMessage(value=value.strip(), attributes=set(), source=source)
                # Keep the last definition so duplicate IDs remain visible through normal
                # Fluent validation while this audit reflects runtime load order as closely
                # as a filesystem-only scan can.
                messages[key] = current
                continue

            attr_match = FTL_ATTR_RE.match(line)
            if attr_match and current is not None:
                current.attributes.add(attr_match.group(1))
    return messages


def effective_field(
    prototype_id: str,
    field: str,
    entities: dict[str, EntityPrototype],
    seen: set[str] | None = None,
) -> tuple[str | None, str | None]:
    """Return (value, source_prototype_id) following entity parent inheritance."""
    entity = entities.get(prototype_id)
    if entity is None:
        return None, None

    value = getattr(entity, field)
    if value is not None:
        return value, prototype_id

    if seen is None:
        seen = set()
    if prototype_id in seen:
        return None, None
    seen = set(seen)
    seen.add(prototype_id)

    for parent in entity.parents:
        value, source_id = effective_field(parent, field, entities, seen)
        if value is not None:
            return value, source_id
    return None, None


def make_report(scope: str | None = None) -> dict[str, object]:
    entities = load_entities()
    messages = load_fluent_messages()
    issues: list[dict[str, object]] = []

    for prototype_id in sorted(entities):
        entity = entities[prototype_id]
        if entity.abstract:
            continue
        if scope and scope not in entity.source:
            continue

        key = f"ent-{prototype_id}"
        message = messages.get(key)
        effective_name, name_source = effective_field(prototype_id, "name", entities)
        effective_desc, desc_source = effective_field(prototype_id, "description", entities)

        missing: list[str] = []
        if effective_name is not None and (message is None or not message.value):
            missing.append("name")
        if effective_desc is not None and (message is None or "desc" not in message.attributes):
            missing.append("desc")
        if not missing:
            continue

        issues.append(
            {
                "id": prototype_id,
                "key": key,
                "missing": missing,
                "source": entity.source,
                "line": entity.line,
                "name": effective_name,
                "name_source": name_source,
                "description": effective_desc,
                "description_source": desc_source,
                "ru_source": message.source if message is not None else None,
            }
        )

    return {
        "prototype_count": len(entities),
        "issue_count": len(issues),
        "scope": scope,
        "issues": issues,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--scope",
        help="Only report prototypes whose repository-relative source path contains this text.",
    )
    parser.add_argument("--json", action="store_true", help="Print the full report as JSON.")
    parser.add_argument(
        "--output",
        type=Path,
        help="Write the full JSON report to this path in addition to the console summary.",
    )
    parser.add_argument(
        "--no-fail",
        action="store_true",
        help="Return success even when localization gaps are found.",
    )
    args = parser.parse_args()

    report = make_report(args.scope)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
    else:
        issues = report["issues"]
        print(
            f"Audited {report['prototype_count']} entity prototypes; "
            f"found {report['issue_count']} ru-RU localization gaps"
            + (f" in scope {args.scope!r}." if args.scope else ".")
        )
        for issue in issues:
            missing = ",".join(issue["missing"])
            print(f"{issue['key']} [{missing}] {issue['source']}:{issue['line']}")

    return 0 if args.no_fail or report["issue_count"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())