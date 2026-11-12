#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Adversarial fixtures for the offline ADR lifecycle validator (T003/S22)."""
from __future__ import annotations

import copy
import json
import shutil
import tempfile
import unittest
from pathlib import Path

from validate_adr import validate

SOURCE = Path(__file__).resolve().parent.parent
RID = "ADR-0011"


class AdrContractTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for name in (
            "docs/requirements/task-registry.csv",
            "docs/decisions/adr-v1.schema.json",
            f"docs/decisions/records/{RID}.json",
            "docs/decisions/ADR-0011-governance-records.md",
        ):
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(SOURCE / name, target)
        self.path = self.root / f"docs/decisions/records/{RID}.json"

    def record(self) -> dict:
        return json.loads(self.path.read_text(encoding="utf-8"))

    def save(self, obj: dict) -> None:
        self.path.write_text(json.dumps(obj, indent=2) + "\n", encoding="utf-8")

    def problems(self) -> list[str]:
        return validate(self.root)

    def expect_failure(self, fragment: str) -> None:
        errors = self.problems()
        self.assertTrue(any(fragment in item for item in errors), f"wanted {fragment!r}, got {errors}")

    @staticmethod
    def receipt(user: str = "reviewer-a", *, revision: int = 1) -> dict:
        # Synthetic fixture; DOES NOT assert any real GitHub review exists.
        return {
            "reviewUrl": f"https://github.com/example/archpulse/pull/42#pullrequestreview-{122+revision}",
            "reviewer": user,
            "commitSha": str(revision) * 40,
            "date": "2026-11-11" if revision == 1 else "2026-11-12",
            "decision": "Approved",
        }

    def make_accepted(self, record: dict) -> None:
        record["status"] = "Accepted"
        record["dates"]["accepted"] = "2026-11-11"
        record["reviewers"] = ["reviewer-a"]
        record["approval"] = self.receipt()
        record["verification"]["ciUrl"] = "https://github.com/example/archpulse/actions/runs/42"

    def test_valid_proposed_example(self):
        self.assertEqual(self.problems(), [])

    def test_required_schema_fields_cover_example(self):
        schema = json.loads((self.root / "docs/decisions/adr-v1.schema.json").read_text())
        self.assertEqual(set(schema["required"]), set(self.record()))
        self.assertEqual(set(schema["properties"]), set(self.record()))

    def test_reject_unknown_task(self):
        d = self.record()
        d["source"]["task"] = "T999"
        self.save(d)
        self.expect_failure("unknown or mismatched task/spec")

    def test_reject_task_spec_disagreement(self):
        d = self.record()
        d["source"]["specification"] = "S01"
        self.save(d)
        self.expect_failure("unknown or mismatched task/spec")

    def test_reject_missing_alternative(self):
        d = self.record()
        d["alternatives"].pop()
        self.save(d)
        self.expect_failure("at least two alternatives")

    def test_reject_duplicate_alternative_id(self):
        d = self.record()
        d["alternatives"][1]["id"] = "A"
        self.save(d)
        self.expect_failure("duplicate alternative IDs")

    def test_reject_missing_evidence_link(self):
        d = self.record()
        d["alternatives"][0]["evidence"] = []
        self.save(d)
        self.expect_failure("evidence")

    def test_reject_invalid_mermaid(self):
        d = self.record()
        d["alternatives"][0]["mermaid"] = "this is definitely not a mermaid diagram"
        self.save(d)
        self.expect_failure("unsupported or missing Mermaid graph")

    def test_reject_diagram_narrative_drift(self):
        d = self.record()
        d["alternatives"][0]["mermaid"] += "\n  History --> End"
        self.save(d)
        self.expect_failure("diagram differs from Markdown")

    def test_reject_missing_narrative(self):
        (self.root / "docs/decisions/ADR-0011-governance-records.md").unlink()
        self.expect_failure("missing/unreadable narrative")

    def test_reject_traversal_in_document(self):
        d = self.record()
        d["document"] = "../secret.md"
        self.save(d)
        self.expect_failure("invalid or mismatched Markdown path")

    def test_reject_impossible_date(self):
        d = self.record()
        d["dates"]["proposed"] = "2026-02-31"
        self.save(d)
        self.expect_failure("invalid proposal date")

    def test_reject_extra_unknown_property(self):
        d = self.record()
        d["secretToggle"] = True
        self.save(d)
        self.expect_failure("unexpected")

    def test_reject_forged_proposal_approval(self):
        d = self.record()
        d["approval"] = self.receipt()
        self.save(d)
        self.expect_failure("Proposed cannot claim approval")

    def test_reject_accepted_without_receipt(self):
        d = self.record()
        d["status"] = "Accepted"
        self.save(d)
        self.expect_failure("expected object")

    def test_accept_synthetic_approved_shape(self):
        d = self.record()
        self.make_accepted(d)
        self.save(d)
        self.assertEqual(self.problems(), [])

    def test_reject_acceptance_without_ci_url(self):
        d = self.record()
        self.make_accepted(d)
        d["verification"]["ciUrl"] = None
        self.save(d)
        self.expect_failure("must link CI")

    def test_reject_self_approval(self):
        d = self.record()
        self.make_accepted(d)
        d["author"] = "reviewer-a"
        self.save(d)
        self.expect_failure("self-approval")

    def test_reject_unreviewed_security_gate(self):
        d = self.record()
        self.make_accepted(d)
        d["gates"]["securityRequired"] = True
        self.save(d)
        self.expect_failure("requires security evidence")

    def test_reject_wrong_approval_decision(self):
        d = self.record()
        self.make_accepted(d)
        d["approval"]["decision"] = "Commented"
        self.save(d)
        self.expect_failure("explicit Approved")

    def test_reject_superseded_without_successor(self):
        d = self.record()
        self.make_accepted(d)
        d["status"] = "Superseded"
        d["supersessionApproval"] = self.receipt(revision=2)
        d["supersededBy"] = "ADR-0099"
        self.save(d)
        self.expect_failure("not a recorded Accepted ADR")

    def test_reject_superseded_without_review(self):
        d = self.record()
        self.make_accepted(d)
        d["status"] = "Superseded"
        d["supersededBy"] = "ADR-0012"
        self.save(d)
        self.expect_failure("supersessionApproval")

    def test_accept_supersession_to_different_accepted_record(self):
        successor = copy.deepcopy(self.record())
        successor["id"] = "ADR-0012"
        successor["document"] = "docs/decisions/ADR-0012-governance-records.md"
        self.make_accepted(successor)
        (self.root / "docs/decisions/records/ADR-0012.json").write_text(
            json.dumps(successor, indent=2) + "\n", encoding="utf-8")
        source_md = (self.root / "docs/decisions/ADR-0011-governance-records.md").read_text()
        (self.root / "docs/decisions/ADR-0012-governance-records.md").write_text(
            source_md.replace("ADR-0011", "ADR-0012"))
        predecessor = self.record()
        self.make_accepted(predecessor)
        predecessor["status"] = "Superseded"
        predecessor["supersessionApproval"] = self.receipt(revision=2)
        predecessor["supersededBy"] = "ADR-0012"
        self.save(predecessor)
        self.assertEqual(self.problems(), [])

    def test_reject_reused_supersession_review(self):
        d = self.record()
        self.make_accepted(d)
        d["status"] = "Superseded"
        d["supersessionApproval"] = self.receipt()
        d["supersededBy"] = "ADR-0012"
        self.save(d)
        self.expect_failure("distinct revision and review")

    def test_reject_unlisted_supersession_reviewer(self):
        d = self.record()
        self.make_accepted(d)
        d["status"] = "Superseded"
        d["supersessionApproval"] = self.receipt("unknown", revision=2)
        d["supersededBy"] = "ADR-0012"
        self.save(d)
        self.expect_failure("supersession reviewer must be declared")

    def test_reject_invalid_json(self):
        self.path.write_text("{ broken", encoding="utf-8")
        self.expect_failure("invalid JSON")

    def test_reject_missing_registry(self):
        (self.root / "docs/requirements/task-registry.csv").unlink()
        self.expect_failure("task traceability registry unavailable")


if __name__ == "__main__":
    unittest.main()
