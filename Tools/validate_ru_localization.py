#!/usr/bin/env python3
"""Fail when a literal Loc.GetString/TryGetString ID has no ru-RU Fluent entry."""

from __future__ import annotations

import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOCALE_ROOT = ROOT / "Resources" / "Locale" / "ru-RU"
FTL_ID = re.compile(r"^([A-Za-z][A-Za-z0-9_-]*)\s*=", re.MULTILINE)
LITERAL_LOC_CALL = re.compile(
    r'\bLoc\.(?:GetString|TryGetString)\(\s*"([A-Za-z][A-Za-z0-9_-]*)"\s*(?:,|\))'
)
SOURCE_ROOTS = (
    "Content.Client",
    "Content.Server",
    "Content.Shared",
    "RobustToolbox/Robust.Client",
    "RobustToolbox/Robust.Server",
    "RobustToolbox/Robust.Shared",
)


def localized_ids() -> set[str]:
    result: set[str] = set()
    for file in LOCALE_ROOT.rglob("*.ftl"):
        result.update(FTL_ID.findall(file.read_text(encoding="utf-8")))
    return result


def literal_localization_references() -> dict[str, list[str]]:
    references: dict[str, list[str]] = defaultdict(list)
    for source_root in SOURCE_ROOTS:
        for file in (ROOT / source_root).rglob("*.cs"):
            for line_number, line in enumerate(file.read_text(encoding="utf-8").splitlines(), 1):
                code = line.split("//", 1)[0]
                for key in LITERAL_LOC_CALL.findall(code):
                    references[key].append(f"{file.relative_to(ROOT)}:{line_number}")
    return references


def main() -> int:
    available = localized_ids()
    references = literal_localization_references()
    missing = sorted(set(references) - available)

    if not missing:
        print(f"OK: {len(references)} literal localization IDs have ru-RU entries.")
        return 0

    print(f"ERROR: {len(missing)} literal localization IDs are missing from ru-RU:", file=sys.stderr)
    for key in missing:
        print(f"  {key}: {references[key][0]}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
