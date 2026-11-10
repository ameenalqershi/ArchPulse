#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Foundation registry and handoff contract tests (not product behavior tests)."""
from __future__ import annotations

import csv
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIREMENTS = ROOT / "docs/requirements"
HANDOFF = ROOT / "docs/operations/agent-handoff"
TASK_ID = re.compile(r"^T\d{3}$")
SPEC_ID = re.compile(r"^S\d{2}$")
REQ_ID = re.compile(r"^R-[A-Z0-9-]+$")
PATTERN_TASK = re.compile(r"T\d{3}")


def registry(name: str) -> list[dict[str, str]]:
    with (REQUIREMENTS / name).open(newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


class FoundationTraceabilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.tasks = registry("task-registry.csv")
        cls.requirements = registry("requirements.csv")
        cls.task_ids = {row["Work ID"].strip() for row in cls.tasks}
        cls.spec_ids = set(re.findall(
            r"^\| (S\d{2}) \|",
            (REQUIREMENTS / "specification-index.md").read_text(encoding="utf-8"),
            flags=re.MULTILINE,
        ))

    def test_all_historical_specs_present(self) -> None:
        self.assertEqual(self.spec_ids, {f"S{i:02d}" for i in range(34)})

    def test_tasks_have_unique_stable_ids(self) -> None:
        ids = [x["Work ID"].strip() for x in self.tasks]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(len(ids), 205)  # Public S26 historical snapshot; revisit when synced
        self.assertTrue(all(TASK_ID.fullmatch(i) for i in ids))

    def test_every_task_specification_exists(self) -> None:
        for task in self.tasks:
            with self.subTest(task=task["Work ID"]):
                self.assertIn(task["Specification"].strip(), self.spec_ids)

    def test_task_dependencies_exist_and_do_not_reference_self(self) -> None:
        for task in self.tasks:
            current = task["Work ID"].strip()
            for dep in PATTERN_TASK.findall(task["Depends On IDs"]):
                with self.subTest(task=current, dependency=dep):
                    self.assertIn(dep, self.task_ids)
                    self.assertNotEqual(current, dep)

    def test_requirement_ids_unique_and_scoped(self) -> None:
        ids = [x["Requirement ID"].strip() for x in self.requirements]
        self.assertEqual(len(ids), 192)  # Historical index, not product implementation
        self.assertEqual(len(ids), len(set(ids)))
        self.assertTrue(all(REQ_ID.fullmatch(i) for i in ids))

    def test_requirement_task_references_exist(self) -> None:
        for requirement in self.requirements:
            for tid in PATTERN_TASK.findall(requirement["Notion tasks"]):
                with self.subTest(requirement=requirement["Requirement ID"], task=tid):
                    self.assertIn(tid, self.task_ids)

    def test_task_notions_links_and_statuses_are_explicit(self) -> None:
        for row in self.tasks:
            with self.subTest(task=row["Work ID"]):
                self.assertTrue(row["Notion URL"].startswith("https://app.notion.com/p/"))
                self.assertIn(row["Status in Notion"], {
                    "Planned", "Ready", "In Progress", "Blocked", "In Review", "Verified",
                })

    def test_handoff_schema_and_audit_receipt_agree(self) -> None:
        schema = json.loads((HANDOFF / "handoff.schema.json").read_text(encoding="utf-8"))
        handoff = json.loads((HANDOFF / "FOUNDATION-AUDIT-2026-11-10.json").read_text(encoding="utf-8"))
        self.assertEqual(handoff["schema"], "archpulse-agent-handoff/v1")
        self.assertEqual(set(handoff), set(schema["required"]))
        self.assertEqual(set(schema["required"]), set(schema["properties"]))
        self.assertTrue(TASK_ID.fullmatch(handoff["taskId"]))
        self.assertTrue(SPEC_ID.fullmatch(handoff["specification"]))
        self.assertIn(handoff["status"], schema["properties"]["status"]["enum"])
        self.assertIn(handoff["taskId"], self.task_ids)
        self.assertIn(handoff["specification"], self.spec_ids)
        for field in ("baseCommit", "workingCommit"):
            self.assertTrue(handoff[field] is None or re.fullmatch(r"[0-9a-f]{40}", handoff[field]))
        self.assertIsNone(handoff["workingCommit"], "Proposed branch commit must not be fabricated")


if __name__ == "__main__":
    unittest.main()
