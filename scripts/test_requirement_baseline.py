#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Positive and adversarial tests for the public S26 requirement lock."""
from __future__ import annotations

import csv
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
from validate_requirement_baseline import validate  # noqa: E402

SOURCE = SCRIPT_DIR.parent / "docs/requirements"


class RequirementBaselineTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="archpulse-requirement-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shutil.copytree(SOURCE, self.root / "docs/requirements")

    def update_row(self, target: str, column: str, value: str) -> None:
        path = self.root / "docs/requirements/requirements.csv"
        with path.open(encoding="utf-8", newline="") as f:
            reader = csv.DictReader(f)
            fieldnames = list(reader.fieldnames or [])
            rows = list(reader)
        for row in rows:
            if row["Requirement ID"] == target:
                row[column] = value
                break
        else:
            self.fail("test fixture is missing target")
        with path.open("w", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            writer.writeheader()
            writer.writerows(rows)

    def test_published_snapshot_is_consistent(self) -> None:
        self.assertEqual([], validate(self.root))

    def test_replacing_stable_id_without_changing_count_is_rejected(self) -> None:
        self.update_row("R-CORE-001", "Requirement ID", "R-CORE-999")
        self.assertTrue(any("requirement ID drift" in x for x in validate(self.root)))

    def test_unreviewed_requirement_removal_is_rejected(self) -> None:
        path = self.root / "docs/requirements/requirements.csv"
        text = path.read_text(encoding="utf-8")
        path.write_text("\n".join(text.splitlines()[:-1]) + "\n", encoding="utf-8")
        self.assertTrue(any("expected 192" in x for x in validate(self.root)))

    def test_wrong_delimiter_cannot_hide_task_gap(self) -> None:
        self.update_row("R-CORE-001", "Notion tasks", "T001;T111")
        self.assertTrue(any("malformed" in x for x in validate(self.root)))

    def test_duplicate_task_edge_is_rejected(self) -> None:
        self.update_row("R-CORE-001", "Notion tasks", "T001,T001")
        self.assertTrue(any("duplicate task" in x for x in validate(self.root)))

    def test_unknown_task_is_rejected(self) -> None:
        self.update_row("R-CORE-001", "Notion tasks", "T999")
        self.assertTrue(any("unknown task" in x for x in validate(self.root)))

    def test_missing_acceptance_evidence_is_rejected(self) -> None:
        self.update_row("R-CORE-001", "Acceptance evidence required", "TBD")
        self.assertTrue(any("no actionable" in x for x in validate(self.root)))

    def test_wrong_s33_task_reference_is_rejected(self) -> None:
        self.update_row("R-TJ-001", "Notion tasks", "T001")
        self.assertTrue(any("no linked task traces to S33" in x for x in validate(self.root)))

    def test_lock_cannot_silently_claim_implementation(self) -> None:
        path = self.root / "docs/requirements/requirement-baseline-lock.json"
        lock = json.loads(path.read_text(encoding="utf-8"))
        lock["designStatus"] = "verified"
        path.write_text(json.dumps(lock), encoding="utf-8")
        self.assertTrue(any("must remain proposed" in x for x in validate(self.root)))


if __name__ == "__main__":
    unittest.main()
