#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Regression and negative tests for the S/T/R index validation gate."""
from __future__ import annotations

import csv
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
from validate_traceability import validate  # noqa: E402

REPOSITORY_ROOT = SCRIPT_DIR.parent


class TraceabilityValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.workspace = tempfile.TemporaryDirectory(prefix="archpulse-trace-")
        self.addCleanup(self.workspace.cleanup)
        self.root = Path(self.workspace.name)
        shutil.copytree(REPOSITORY_ROOT / "docs/requirements",
                        self.root / "docs/requirements")

    def _update(self, filename: str, selector: str, column: str, value: str) -> None:
        path = self.root / "docs/requirements" / filename
        with path.open(encoding="utf-8", newline="") as stream:
            reader = csv.DictReader(stream)
            fields = reader.fieldnames
            rows = list(reader)
        assert fields is not None
        row = next(item for item in rows if item[fields[0]] == selector)
        row[column] = value
        with path.open("w", encoding="utf-8", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=fields)
            writer.writeheader()
            writer.writerows(rows)

    def test_current_baseline_has_consistent_references(self) -> None:
        self.assertEqual([], validate(self.root))

    def test_missing_task_is_detected(self) -> None:
        path = self.root / "docs/requirements/task-registry.csv"
        lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
        path.write_text("".join(lines[:-1]), encoding="utf-8")
        self.assertTrue(any("expected 205" in e for e in validate(self.root)))

    def test_duplicate_task_id_is_detected(self) -> None:
        self._update("task-registry.csv", "T002", "Work ID", "T001")
        self.assertTrue(any("duplicate" in e.lower() for e in validate(self.root)))

    def test_dangling_dependency_is_detected(self) -> None:
        self._update("task-registry.csv", "T002", "Depends On IDs", "T999")
        self.assertTrue(any("dangling dependency" in e for e in validate(self.root)))

    def test_dependency_cycle_is_detected(self) -> None:
        self._update("task-registry.csv", "T001", "Depends On IDs", "T002")
        self.assertTrue(any("cycle" in e.lower() for e in validate(self.root)))

    def test_invalid_specification_is_detected(self) -> None:
        self._update("task-registry.csv", "T001", "Specification", "S99")
        self.assertTrue(any("unrecognized specification" in e for e in validate(self.root)))

    def test_dangling_requirement_task_is_detected(self) -> None:
        self._update("requirements.csv", "R-CORE-001", "Notion tasks", "T999")
        self.assertTrue(any("dangling or missing task" in e for e in validate(self.root)))

    def test_claim_of_verification_without_evidence_is_rejected(self) -> None:
        self._update("requirements.csv", "R-CORE-001", "Implementation status", "Verified")
        self.assertTrue(any("no evidence field" in e for e in validate(self.root)))


if __name__ == "__main__":
    unittest.main()
