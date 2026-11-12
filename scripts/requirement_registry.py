#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Deterministic, read-only machine-readable view of the proposed S26 registry.

Does not implement ArchPulse features or attest to requirement acceptance.
Run both existing integrity validators before producing any output.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

from validate_requirement_baseline import validate as validate_baseline
from validate_traceability import validate as validate_traceability

ROOT = Path(__file__).resolve().parent.parent
SPECS = re.compile(
    r"^\|\s*(S\d{2})\s*\|\s*\[([^\]]+)\]\((https://app\.notion\.com/p/[a-f0-9]+)\)"
    r"\s*\|\s*([^|]+)\|\s*([^|]+)\|\s*([^|]+)\|"
)
TASK_ID = re.compile(r"T\d{3}")
INPUT_FILES = (
    "docs/requirements/requirements.csv",
    "docs/requirements/task-registry.csv",
    "docs/requirements/specification-index.md",
    "docs/requirements/requirement-baseline-lock.json",
)


class RegistryError(ValueError):
    """A prerequisite or registry invariant did not pass validation."""


def _csv(root: Path, name: str) -> list[dict[str, str]]:
    with (root / "docs/requirements" / name).open(
        encoding="utf-8-sig", newline=""
    ) as stream:
        return list(csv.DictReader(stream))


def _specifications(root: Path) -> dict[str, dict[str, str]]:
    rows: dict[str, dict[str, str]] = {}
    source = (root / "docs/requirements/specification-index.md").read_text(
        encoding="utf-8"
    )
    for line in source.splitlines():
        match = SPECS.match(line)
        if match:
            key, title, url, domain, priority, approval = match.groups()
            if key in rows:
                raise RegistryError(f"duplicate specification {key}")
            rows[key] = {
                "id": key,
                "title": title,
                "notionUrl": url,
                "domain": domain.strip(),
                "priority": priority.strip(),
                "approval": approval.strip(),
            }
    if set(rows) != {f"S{i:02}" for i in range(34)}:
        raise RegistryError("missing or unexpected specification metadata")
    return rows


def _fingerprint(root: Path) -> str:
    digest = hashlib.sha256()
    for name in INPUT_FILES:
        digest.update(name.encode("utf-8"))
        digest.update(b"\0")
        digest.update((root / name).read_bytes())
        digest.update(b"\0")
    return digest.hexdigest()


def build_manifest(root: Path = ROOT) -> dict[str, Any]:
    """Construct from the public snapshot; no network, side effects, or timestamps."""
    failures = validate_baseline(root) + validate_traceability(root)
    if failures:
        raise RegistryError("; ".join(failures[:15]))
    lock = json.loads((root / INPUT_FILES[-1]).read_text(encoding="utf-8"))
    specs = _specifications(root)
    task_rows = _csv(root, "task-registry.csv")
    tasks = {row["Work ID"]: row for row in task_rows}
    if len(tasks) != len(task_rows):
        raise RegistryError("duplicate tasks in source registry")
    requirements = []
    for source in sorted(_csv(root, "requirements.csv"), key=lambda row: row["Requirement ID"]):
        rid = source["Requirement ID"]
        family = rid.rsplit("-", 1)[0]
        owner = lock["familyPrimarySpecifications"][family]
        refs = source["Notion tasks"].split(",")
        linked_tasks = []
        for tid in refs:
            item = tasks[tid]
            linked_tasks.append({
                "id": tid,
                "sourceSpecification": item["Specification"],
                "notionUrl": item["Notion URL"],
                "dependencies": TASK_ID.findall(item["Depends On IDs"]),
                "status": item["Status in Notion"],
                "gate": item["Gate"],
                "priority": item["Priority"],
            })
        linked_specs = sorted({item["sourceSpecification"] for item in linked_tasks})
        requirements.append({
            "id": rid,
            "behavior": source["Expected behavior"],
            "source": {"specification": "S26", "notionUrl": lock["source"]},
            "candidateOwnerSpecification": specs[owner],
            "ownershipApproval": "Pending",
            "linkedSpecificationIds": linked_specs,
            "linkedTasks": linked_tasks,
            "scope": {
                "productTarget": "MandatoryFullProduct",
                "designApproval": "Proposed",
                "implementation": source["Implementation status"],
                "verification": "Unknown",
            },
            "evidence": {
                "expected": source["Acceptance evidence required"],
                "receipts": [],
            },
        })
    if [x["id"] for x in requirements] != lock["requirementIds"]:
        raise RegistryError("produced manifest does not match reviewed ID lock")
    return {
        "schema": "archpulse-requirements-registry/v1",
        "basis": {
            "sourceSpecification": "S26",
            "sourceNotionUrl": lock["source"],
            "sourceLastEditedUtc": lock["sourceLastEditedUtc"],
            "contentSha256": _fingerprint(root),
            "status": "Proposed",
            "notLiveSynchronized": True,
        },
        "counts": {
            "specifications": len(specs),
            "tasks": len(tasks),
            "requirements": len(requirements),
            "requirementFamilies": len(lock["familyPrimarySpecifications"]),
        },
        "requirements": requirements,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument("--json", action="store_true", help="Print complete JSON registry")
    selection.add_argument("--requirement", metavar="R-ID", help="Print one requirement entry")
    args = parser.parse_args(argv)
    try:
        manifest = build_manifest()
    except (RegistryError, OSError, ValueError, KeyError) as exc:
        print(f"REQUIREMENTS REGISTRY INVALID: {exc}", file=sys.stderr)
        return 1
    if args.requirement:
        match = next((row for row in manifest["requirements"]
                      if row["id"] == args.requirement), None)
        if match is None:
            print(f"Requirement not found: {args.requirement}", file=sys.stderr)
            return 2
        print(json.dumps(match, ensure_ascii=False, indent=2))
    elif args.json:
        print(json.dumps(manifest, ensure_ascii=False, indent=2))
    else:
        c = manifest["counts"]
        print(
            f"REGISTRY VALIDATED: {c['requirements']} planned requirements, "
            f"{c['tasks']} tasks, {c['specifications']} proposed specifications; "
            "no implementation claims"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
