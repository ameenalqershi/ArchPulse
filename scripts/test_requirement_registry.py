#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Positive/negative tests for the deterministic proposed requirement manifest."""
from __future__ import annotations

import contextlib
import csv
import io
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPTS))
from requirement_registry import RegistryError, build_manifest, main  # noqa: E402

ROOT = SCRIPTS.parent


class RegistryManifestTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.manifest = build_manifest()

    def test_canonical_counts_and_ordering(self) -> None:
        counts = self.manifest["counts"]
        self.assertEqual(counts, {
            "specifications": 34,
            "tasks": 205,
            "requirements": 192,
            "requirementFamilies": 21,
        })
        ids = [r["id"] for r in self.manifest["requirements"]]
        self.assertEqual(ids, sorted(ids))
        self.assertEqual(len(ids), len(set(ids)))

    def test_all_sources_and_owner_candidates_are_grounded(self) -> None:
        for r in self.manifest["requirements"]:
            with self.subTest(id=r["id"]):
                self.assertEqual(r["source"]["specification"], "S26")
                self.assertTrue(r["source"]["notionUrl"].startswith("https://app.notion.com/p/"))
                self.assertTrue(r["candidateOwnerSpecification"]["id"].startswith("S"))
                self.assertTrue(r["candidateOwnerSpecification"]["notionUrl"].startswith("https://app.notion.com/p/"))
                self.assertEqual(r["ownershipApproval"], "Pending")
                self.assertTrue(r["linkedTasks"])
                self.assertEqual(
                    sorted({t["sourceSpecification"] for t in r["linkedTasks"]}),
                    r["linkedSpecificationIds"],
                )

    def test_full_product_target_is_distinct_from_design_approval(self) -> None:
        for r in self.manifest["requirements"]:
            self.assertEqual(r["scope"], {
                "productTarget": "MandatoryFullProduct",
                "designApproval": "Proposed",
                "implementation": "Planned",
                "verification": "Unknown",
            })
            self.assertEqual(r["evidence"]["receipts"], [])
            self.assertTrue(r["evidence"]["expected"])

    def test_tj_crosswalk_and_task_dependencies(self) -> None:
        r = next(x for x in self.manifest["requirements"] if x["id"] == "R-TJ-020")
        self.assertEqual(r["candidateOwnerSpecification"]["id"], "S33")
        self.assertIn("S33", r["linkedSpecificationIds"])
        self.assertTrue(all(t["id"].startswith("T") for t in r["linkedTasks"]))
        self.assertTrue(all(isinstance(t["dependencies"], list) for t in r["linkedTasks"]))

    def test_cross_specification_integration_tasks_are_preserved(self) -> None:
        row = next(x for x in self.manifest["requirements"] if x["id"] == "R-VGOV-015")
        self.assertEqual(row["candidateOwnerSpecification"]["id"], "S28")
        self.assertEqual(row["linkedSpecificationIds"], ["S28", "S29"])
        self.assertEqual([t["id"] for t in row["linkedTasks"]], ["T150", "T160"])

    def test_repeated_export_is_byte_deterministic(self) -> None:
        a = json.dumps(build_manifest(), sort_keys=True, ensure_ascii=False)
        b = json.dumps(build_manifest(), sort_keys=True, ensure_ascii=False)
        self.assertEqual(a.encode("utf-8"), b.encode("utf-8"))
        self.assertEqual(len(self.manifest["basis"]["contentSha256"]), 64)
        self.assertTrue(self.manifest["basis"]["notLiveSynchronized"])

    def test_cli_single_id_is_machine_readable(self) -> None:
        captured = io.StringIO()
        with contextlib.redirect_stdout(captured):
            code = main(["--requirement", "R-CORE-001"])
        self.assertEqual(code, 0)
        row = json.loads(captured.getvalue())
        self.assertEqual(row["id"], "R-CORE-001")
        self.assertEqual(row["linkedTasks"][0]["id"], "T001")

    def test_cli_unknown_id_fails_with_distinct_exit(self) -> None:
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(main(["--requirement", "R-CORE-999"]), 2)

    def test_cli_json_output_matches_builder(self) -> None:
        with contextlib.redirect_stdout(io.StringIO()) as captured:
            self.assertEqual(main(["--json"]), 0)
        self.assertEqual(json.loads(captured.getvalue()), self.manifest)

    def test_invalid_status_prevents_export(self) -> None:
        with tempfile.TemporaryDirectory(prefix="archpulse-manifest-") as tmp:
            root = Path(tmp)
            shutil.copytree(ROOT / "docs/requirements", root / "docs/requirements")
            path = root / "docs/requirements/requirements.csv"
            with path.open(newline="", encoding="utf-8") as inp:
                reader = csv.DictReader(inp)
                fields = list(reader.fieldnames or [])
                rows = list(reader)
            rows[0]["Implementation status"] = "Verified"
            with path.open("w", newline="", encoding="utf-8") as out:
                writer = csv.DictWriter(out, fields)
                writer.writeheader()
                writer.writerows(rows)
            with self.assertRaisesRegex(RegistryError, "implementation"):
                build_manifest(root)


if __name__ == "__main__":
    unittest.main()
