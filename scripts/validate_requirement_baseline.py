#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Guard the published S26 requirement-ID snapshot against silent scope loss.

The lock records *proposed* requirements; passing is not evidence of implementation.
Changing the lock requires review against canonical Notion S26, not auto-regeneration.
"""
from __future__ import annotations

import csv
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOCK = "docs/requirements/requirement-baseline-lock.json"
SOURCE = "https://app.notion.com/p/4143830ba34d8185a0edc83f974c55cd"
IDENTIFIER = re.compile(r"^(R-[A-Z][A-Z0-9-]*)-\d{3}$")
TASKS = re.compile(r"^T\d{3}(?:,T\d{3})*$")
NOT_READY = {"tbd", "todo", "n/a", "none", "unknown", "—", "pending"}
DETAILED = {
    "R-NAV": "S27",
    "R-VGOV": "S28",
    "R-MCP-DEC": "S29",
    "R-BCANVAS": "S30",
    "R-ITEST": "S31",
    "R-MERMAID": "S32",
    "R-TJ": "S33",
}


def _read_csv(path: Path, errors: list[str]) -> list[dict[str, str]]:
    try:
        with path.open(encoding="utf-8-sig", newline="") as stream:
            return list(csv.DictReader(stream))
    except (OSError, UnicodeError, csv.Error) as exc:
        errors.append(f"{path.name}: cannot read: {exc}")
        return []


def validate(root: Path = ROOT) -> list[str]:
    errors: list[str] = []
    path = root / LOCK
    try:
        lock = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, ValueError) as exc:
        return [f"{LOCK}: unreadable lock: {exc}"]
    if not isinstance(lock, dict):
        return [f"{LOCK}: must be a JSON object"]
    if lock.get("schema") != "archpulse-requirement-baseline/v1":
        errors.append("lock: unsupported schema")
    if lock.get("source") != SOURCE or lock.get("sourceSpecification") != "S26":
        errors.append("lock: canonical S26 provenance missing")
    if lock.get("designStatus") != "proposed":
        errors.append("lock: baseline must remain proposed (not implemented)")
    if lock.get("snapshotCount") != 192:
        errors.append("lock: current reviewed snapshot has 192 requirements")
    ids = lock.get("requirementIds")
    if not isinstance(ids, list) or any(not isinstance(x, str) for x in ids):
        return errors + ["lock: requirementIds must be a string array"]
    if len(ids) != 192 or len(set(ids)) != len(ids) or ids != sorted(ids):
        errors.append("lock: expected 192 unique, sorted requirement IDs")
    if any(not IDENTIFIER.fullmatch(x) for x in ids):
        errors.append("lock: invalid requirement ID")
    mapping = lock.get("familyPrimarySpecifications")
    if not isinstance(mapping, dict) or any(
        not isinstance(k, str) or not isinstance(v, str) for k, v in mapping.items()
    ):
        return errors + ["lock: familyPrimarySpecifications must be a string map"]
    if any(mapping.get(k) != v for k, v in DETAILED.items()):
        errors.append("lock: S27-S33 family mappings must retain their canonical specs")

    req = _read_csv(root / "docs/requirements/requirements.csv", errors)
    tasks = _read_csv(root / "docs/requirements/task-registry.csv", errors)
    task_specs = {r.get("Work ID"): r.get("Specification") for r in tasks}
    actual_ids = [r.get("Requirement ID", "") for r in req]
    if len(req) != 192 or len(actual_ids) != len(set(actual_ids)):
        errors.append("requirements.csv: expected 192 unique rows")
    missing = sorted(set(ids) - set(actual_ids))
    added = sorted(set(actual_ids) - set(ids))
    if missing or added:
        errors.append(
            "requirement ID drift: missing=" + repr(missing[:8])
            + " unexpected=" + repr(added[:8])
            + " (review Notion S26 before changing lock)"
        )
    families = {m.group(1) for rid in ids if (m := IDENTIFIER.fullmatch(rid))}
    if set(mapping) != families:
        errors.append("lock: each family must have one primary specification")
    valid_specs = {f"S{i:02d}" for i in range(34)}
    for family, spec in mapping.items():
        if spec not in valid_specs:
            errors.append(f"lock: invalid primary specification for {family}: {spec}")

    for row in req:
        rid = row.get("Requirement ID", "")
        match = IDENTIFIER.fullmatch(rid)
        if not match:
            errors.append(f"requirements.csv: invalid ID {rid!r}")
            continue
        family = match.group(1)
        if family not in mapping:
            errors.append(f"{rid}: untracked requirement family")
        task_field = row.get("Notion tasks", "")
        if not TASKS.fullmatch(task_field):
            errors.append(f"{rid}: malformed comma-separated Txxx task references")
            continue
        references = task_field.split(",")
        if len(references) != len(set(references)):
            errors.append(f"{rid}: duplicate task references")
        for ref in references:
            if ref not in task_specs:
                errors.append(f"{rid}: unknown task {ref}")
        # Cross-specification acceptance tasks are valid: one canonical task is
        # required, but additional cross-cutting integration tasks are allowed.
        if family in DETAILED and not any(
            task_specs.get(ref) == DETAILED[family] for ref in references
        ):
            errors.append(f"{rid}: no linked task traces to {DETAILED[family]}")
        for key in ("Expected behavior", "Acceptance evidence required"):
            value = row.get(key, "").strip()
            if not value or value.lower() in NOT_READY:
                errors.append(f"{rid}: no actionable {key}")
        if row.get("Implementation status") not in ("Planned", "Unknown"):
            errors.append(f"{rid}: unsupported implementation claim")
        if row.get("Source specification") != "S26":
            errors.append(f"{rid}: source snapshot must identify S26")
    return errors


if __name__ == "__main__":
    failures = validate()
    if failures:
        print("REQUIREMENT BASELINE FAILED")
        for error in failures:
            print(" -", error)
        raise SystemExit(1)
    print("REQUIREMENT BASELINE VALIDATED: 192 proposed S26 IDs, 21 families, "
          "S27-S33 traceability; this is not product acceptance")
