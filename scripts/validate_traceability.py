#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Validate proposed Sxx/Txxx/R-xxx traceability, not implemented capabilities."""
from __future__ import annotations

import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SPECS = 34
TASKS = 205
REQUIREMENTS = 192
SPEC_PATTERN = re.compile(r"^S\d{2}$")
TASK_PATTERN = re.compile(r"^T\d{3}$")
REQUIREMENT_PATTERN = re.compile(r"^R-[A-Z][A-Z0-9-]*-\d{3}$")
SPEC_REF = re.compile(r"S\d{2}")
TASK_REF = re.compile(r"T\d{3}")


def _read_csv(path: Path, columns: tuple[str, ...], errors: list[str]) -> list[dict[str, str]]:
    try:
        with path.open(newline="", encoding="utf-8-sig") as stream:
            reader = csv.DictReader(stream)
            if reader.fieldnames != list(columns):
                errors.append(f"{path.name}: CSV header differs from documented schema")
                return []
            rows = list(reader)
    except (OSError, UnicodeError, csv.Error) as exc:
        errors.append(f"{path.name}: unreadable CSV: {exc}")
        return []
    for index, row in enumerate(rows, 2):
        if None in row or any(value is None or not value.strip() for value in row.values()):
            errors.append(f"{path.name}:{index}: missing or malformed field")
    return rows


def _check_ids(rows: list[dict[str, str]], field: str, expected: set[str],
               pattern: re.Pattern[str], errors: list[str]) -> set[str]:
    actual = [r[field] for r in rows]
    found = set(actual)
    if len(actual) != len(expected):
        errors.append(f"{field}: expected {len(expected)} rows; got {len(actual)}")
    if len(found) != len(actual):
        errors.append(f"{field}: duplicate identifiers")
    for value in sorted(found):
        if not pattern.fullmatch(value):
            errors.append(f"{field}: invalid ID {value!r}")
    missing = expected - found
    extra = found - expected
    if missing:
        errors.append(f"{field}: missing {','.join(sorted(missing)[:8])}")
    if extra:
        errors.append(f"{field}: unexpected {','.join(sorted(extra)[:8])}")
    return found


def validate(root: Path = ROOT) -> list[str]:
    errors: list[str] = []
    specs_file = root / "docs/requirements/specification-index.md"
    try:
        lines = specs_file.read_text(encoding="utf-8").splitlines()
    except OSError as exc:
        errors.append(f"specification-index.md: {exc}")
        lines = []
    spec_ids = []
    for line in lines:
        match = re.match(r"^\| (S\d{2}) \|", line)
        if match:
            spec_ids.append(match.group(1))
            if "https://app.notion.com/p/" not in line:
                errors.append(f"{match.group(1)}: missing canonical Notion link")
    expected_specs = {f"S{i:02}" for i in range(SPECS)}
    if len(spec_ids) != SPECS or set(spec_ids) != expected_specs:
        errors.append("specification-index.md: missing, duplicate, or out-of-range Sxx")
    if len(spec_ids) != len(set(spec_ids)):
        errors.append("specification-index.md: duplicate Sxx")

    task_columns = ("Work ID", "Task", "Area", "Gate", "Priority", "Status in Notion",
                    "Depends On IDs", "Specification", "Requirement IDs", "Notion URL")
    requirement_columns = ("Requirement ID", "Expected behavior", "Notion tasks",
                           "Acceptance evidence required", "Implementation status",
                           "Source specification")
    tasks = _read_csv(root / "docs/requirements/task-registry.csv", task_columns, errors)
    requirements = _read_csv(root / "docs/requirements/requirements.csv",
                             requirement_columns, errors)
    task_ids = _check_ids(tasks, "Work ID", {f"T{i:03}" for i in range(1, TASKS + 1)},
                          TASK_PATTERN, errors)
    _check_ids(requirements, "Requirement ID",
               {r["Requirement ID"] for r in requirements} if len(requirements) == REQUIREMENTS else set(),
               REQUIREMENT_PATTERN, errors)
    if len(requirements) != REQUIREMENTS:
        errors.append(f"Requirement ID: expected {REQUIREMENTS} rows; got {len(requirements)}")
    if len({r["Requirement ID"] for r in requirements}) != len(requirements):
        errors.append("Requirement ID: duplicate identifiers")

    dependencies: dict[str, list[str]] = {}
    for row in tasks:
        tid = row["Work ID"]
        if row["Status in Notion"] not in ("Planned", "Ready", "In Progress", "Blocked", "In Review"):
            errors.append(f"{tid}: unsupported or unverified task status")
        if row["Priority"] not in ("P0", "P1", "P2"):
            errors.append(f"{tid}: invalid priority")
        if row["Specification"] not in expected_specs:
            errors.append(f"{tid}: unrecognized specification {row['Specification']}")
        if not row["Notion URL"].startswith("https://app.notion.com/p/"):
            errors.append(f"{tid}: invalid Notion URL")
        refs = TASK_REF.findall(row["Depends On IDs"])
        if row["Depends On IDs"] != "—" and not refs:
            errors.append(f"{tid}: unparseable dependencies")
        if any(ref not in task_ids for ref in refs):
            errors.append(f"{tid}: dangling dependency")
        if tid in refs:
            errors.append(f"{tid}: self dependency")
        dependencies[tid] = refs
    visited: set[str] = set()
    visiting: set[str] = set()

    def walk(task: str) -> None:
        if task in visited:
            return
        if task in visiting:
            errors.append(f"Dependency cycle through {task}")
            return
        visiting.add(task)
        for dep in dependencies.get(task, []):
            walk(dep)
        visiting.remove(task)
        visited.add(task)

    for tid in dependencies:
        walk(tid)

    for row in requirements:
        rid = row["Requirement ID"]
        if row["Implementation status"] not in ("Planned", "Unknown"):
            errors.append(f"{rid}: implemented/verified status has no evidence field")
        sources = SPEC_REF.findall(row["Source specification"])
        if not sources or any(s not in expected_specs for s in sources):
            errors.append(f"{rid}: invalid source specification")
        refs = TASK_REF.findall(row["Notion tasks"])
        if not refs or any(t not in task_ids for t in refs):
            errors.append(f"{rid}: dangling or missing task reference")
    return errors


if __name__ == "__main__":
    issues = validate()
    if issues:
        print("TRACEABILITY VALIDATION FAILED")
        for issue in issues:
            print(" -", issue)
        raise SystemExit(1)
    print(f"TRACEABILITY INDEX VALIDATED: {SPECS} proposed specs, "
          f"{TASKS} planned tasks, {REQUIREMENTS} planned requirements; "
          "not proof of implemented functionality")
