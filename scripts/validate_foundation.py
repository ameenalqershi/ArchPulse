#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Deterministic structural validation for foundation, NOT a product readiness claim."""
from pathlib import Path
import csv
import json
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
REQUIRED = [
    "README.md", "README.ar.md", "LICENSE", "CONTRIBUTING.md",
    "CODE_OF_CONDUCT.md", "SECURITY.md", "GOVERNANCE.md", "SUPPORT.md",
    "MAINTAINERS.md", "ROADMAP.md", "AGENTS.md", "ArchPulse.sln",
    ".github/CODEOWNERS", ".github/dependabot.yml", ".github/workflows/ci.yml",
    "docs/requirements/specification-index.md", "docs/requirements/task-registry.csv",
    "docs/requirements/requirements.csv", "docs/operations/agent-handoff/handoff.schema.json",
]
errors = []
for relative in REQUIRED:
    if not (ROOT / relative).is_file():
        errors.append(f"Missing: {relative}")
license_file = ROOT / "LICENSE"
if license_file.is_file():
    text = license_file.read_text(encoding="utf-8")
    if "Apache License" not in text or "Version 2.0, January 2004" not in text:
        errors.append("Root license is not recognized as standard Apache-2.0 text")
for file in ROOT.rglob("*.cs"):
    if any(part in {"bin", "obj"} for part in file.relative_to(ROOT).parts):
        continue
    if "SPDX-License-Identifier: Apache-2.0" not in file.read_text(encoding="utf-8"):
        errors.append(f"SPDX missing: {file.relative_to(ROOT)}")
for suffix, parser in [
    (".json", lambda p: json.loads(p.read_text(encoding="utf-8"))),
    (".csproj", lambda p: ET.parse(p)),
]:
    for file in ROOT.rglob("*" + suffix):
        if any(part in {"bin", "obj"} for part in file.relative_to(ROOT).parts):
            continue
        try:
            parser(file)
        except (ValueError, ET.ParseError) as exc:
            errors.append(f"Invalid {suffix}: {file.relative_to(ROOT)}: {exc}")
for file in ROOT.rglob("*.md"):
    if ".git" in file.parts:
        continue
    content = file.read_text(encoding="utf-8")
    for link in re.findall(r"\[[^]]+\]\(([^)]+)\)", content):
        if link.startswith(("https://", "http://", "mailto:", "#")):
            continue
        relative = link.split("#", 1)[0].split("?", 1)[0]
        if relative and not (file.parent / relative).exists():
            errors.append(f"Broken local link: {file.relative_to(ROOT)} -> {link}")
for registry, expected, field in [
    ("task-registry.csv", 205, "Work ID"),
    ("requirements.csv", 192, "Requirement ID"),
]:
    path = ROOT / "docs" / "requirements" / registry
    if path.exists():
        with path.open(encoding="utf-8", newline="") as fp:
            rows = list(csv.DictReader(fp))
        if len(rows) != expected or len({x[field] for x in rows}) != expected:
            errors.append(f"{registry}: expected {expected} distinct rows, got {len(rows)}")
        if any(r.get("Implementation status", "Planned") not in ("Planned", "Unknown") for r in rows):
            errors.append(f"{registry}: do not fabricate verified statuses")
if errors:
    print("FOUNDATION VALIDATION FAILED")
    for error in errors:
        print(" -", error)
    sys.exit(1)
print("FOUNDATION STRUCTURE VALIDATED — this is not evidence of product functionality")
