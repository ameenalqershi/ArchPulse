#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Validate public S25 development-agent checkpoints; not an authenticity attestation.

This is a dependency-free validator for the repository's fixed handoff/v1 schema,
plus conservative security and provenance invariants. It is not a generic JSON
Schema implementation and it never contacts GitHub or Notion.
"""
from __future__ import annotations

import csv
import json
import re
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
HANDOFF = ROOT / "docs/operations/agent-handoff"
SHA = re.compile(r"[0-9a-f]{40}\Z")
REPOSITORY = re.compile(r"https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\Z")
SECRET_SIGNATURES = (
    re.compile(r"gh[pousr]_[A-Za-z0-9]{30,}"),
    re.compile(r"github_pat_[A-Za-z0-9_]{15,}"),
    re.compile(r"AKIA[A-Z0-9]{16}"),
    re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
)


def registry(root: Path) -> dict[str, str]:
    path = root / "docs/requirements/task-registry.csv"
    with path.open(encoding="utf-8", newline="") as stream:
        return {row["Work ID"]: row["Specification"] for row in csv.DictReader(stream)}


def matches_type(value: Any, kind: str) -> bool:
    return {
        "string": lambda: isinstance(value, str),
        "null": lambda: value is None,
        "object": lambda: isinstance(value, dict),
        "array": lambda: isinstance(value, list),
    }[kind]()


def validate_checkpoint(record: Any, schema: dict[str, Any],
                        task_specs: dict[str, str]) -> list[str]:
    errors: list[str] = []
    if not isinstance(record, dict):
        return ["checkpoint must be a JSON object"]
    required = set(schema["required"])
    properties = schema["properties"]
    for key in sorted(required - record.keys()):
        errors.append(f"missing required field: {key}")
    for key in sorted(record.keys() - properties.keys()):
        errors.append(f"unrecognized field: {key}")
    for key, value in record.items():
        if key not in properties:
            continue
        rule = properties[key]
        types = rule.get("type", [])
        if isinstance(types, str):
            types = [types]
        if types and not any(matches_type(value, t) for t in types):
            errors.append(f"{key}: incorrect JSON type")
            continue
        if "const" in rule and value != rule["const"]:
            errors.append(f"{key}: wrong schema version")
        if "enum" in rule and value not in rule["enum"]:
            errors.append(f"{key}: unknown status")
        if "pattern" in rule and isinstance(value, str):
            if not re.fullmatch(rule["pattern"], value):
                errors.append(f"{key}: invalid identifier")
        if isinstance(value, list) and "items" in rule:
            item_types = rule["items"].get("type", [])
            if isinstance(item_types, str):
                item_types = [item_types]
            for index, item in enumerate(value):
                if not any(matches_type(item, t) for t in item_types):
                    errors.append(f"{key}[{index}]: invalid item type")

    task = record.get("taskId")
    spec = record.get("specification")
    if isinstance(task, str) and task not in task_specs:
        errors.append("taskId: not present in public task registry")
    if isinstance(task, str) and isinstance(spec, str) and task in task_specs:
        if spec != task_specs[task]:
            errors.append("specification: does not match task registry")

    for key in ("baseCommit", "workingCommit"):
        value = record.get(key)
        if value is not None and (not isinstance(value, str) or not SHA.fullmatch(value)):
            errors.append(f"{key}: must be a full lowercase Git commit SHA or null")
    repo = record.get("repository")
    if repo is not None and (not isinstance(repo, str) or not REPOSITORY.fullmatch(repo)):
        errors.append("repository: expected canonical public GitHub repository URL")
    branch = record.get("branch")
    if branch is not None and (
        not isinstance(branch, str) or not branch.strip()
        or any(c.isspace() for c in branch) or branch.startswith("-")
    ):
        errors.append("branch: invalid ref name")

    changed = record.get("changedFiles")
    if isinstance(changed, list):
        for i, path in enumerate(changed):
            if not isinstance(path, str) or (
                not path or path.startswith("/") or "\\" in path
                or any(part in ("", ".", "..") for part in path.split("/"))
            ):
                errors.append(f"changedFiles[{i}]: unsafe repository-relative path")

    status = record.get("status")
    if status in ("review", "verified"):
        if record.get("workingCommit") is None:
            errors.append(f"status={status}: workingCommit must be recorded")
    if status == "review" and not record.get("pullRequests"):
        errors.append("status=review: pullRequests cannot be empty")
    if status == "verified" and not record.get("testResults"):
        errors.append("status=verified: testResults cannot be empty")

    # Catch obvious credential formats. This is *not* a comprehensive secret scan.
    serialized = json.dumps(record, ensure_ascii=False)
    if any(expression.search(serialized) for expression in SECRET_SIGNATURES):
        errors.append("possible credential/private key detected; do not publish checkpoint")
    return errors


def validate_all(root: Path = ROOT) -> list[str]:
    handoff = root / "docs/operations/agent-handoff"
    errors: list[str] = []
    try:
        schema = json.loads((handoff / "handoff.schema.json").read_text(encoding="utf-8"))
        task_specs = registry(root)
    except (OSError, ValueError, KeyError) as exc:
        return [f"unable to read handoff schema or registry: {exc}"]
    for path in sorted(handoff.glob("*.json")):
        if path.name == "handoff.schema.json":
            continue
        try:
            record = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, ValueError) as exc:
            errors.append(f"{path.name}: invalid JSON: {exc}")
            continue
        errors.extend(f"{path.name}: {issue}" for issue in
                      validate_checkpoint(record, schema, task_specs))
    if not list(handoff.glob("FOUNDATION*.json")):
        errors.append("missing public foundation checkpoint")
    return errors


if __name__ == "__main__":
    issues = validate_all()
    if issues:
        print("HANDOFF CHECKPOINT VALIDATION FAILED")
        for issue in issues:
            print(" -", issue)
        sys.exit(1)
    print("HANDOFF CHECKPOINT STRUCTURE VALIDATED — not proof of remote effects, "
          "agent takeover, or absence of all secrets")
