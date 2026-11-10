#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Negative contract tests for the public development-agent handoff validator."""
from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from validate_handoffs import HANDOFF, ROOT, registry, validate_all, validate_checkpoint


class HandoffValidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.schema = json.loads((HANDOFF / "handoff.schema.json").read_text(encoding="utf-8"))
        cls.tasks = registry(ROOT)
        cls.base = json.loads(
            (HANDOFF / "FOUNDATION-PR15-2026-11-10.json").read_text(encoding="utf-8")
        )

    def altered(self, **changes: object) -> list[str]:
        record = copy.deepcopy(self.base)
        record.update(changes)
        return validate_checkpoint(record, self.schema, self.tasks)

    def test_existing_checkpoints_pass(self) -> None:
        self.assertEqual(validate_all(), [])

    def test_unknown_key_rejected(self) -> None:
        self.assertTrue(self.altered(credential="redacted"))

    def test_missing_required_field_rejected(self) -> None:
        record = copy.deepcopy(self.base)
        record.pop("schema")
        self.assertTrue(validate_checkpoint(record, self.schema, self.tasks))

    def test_unknown_task_rejected(self) -> None:
        self.assertIn("taskId: not present in public task registry", self.altered(taskId="T999"))

    def test_mismatched_task_spec_rejected(self) -> None:
        self.assertIn("specification: does not match task registry",
                      self.altered(specification="S11"))

    def test_invalid_sha_rejected(self) -> None:
        self.assertTrue(self.altered(workingCommit="deadbeef"))

    def test_null_commit_cannot_claim_review(self) -> None:
        self.assertTrue(self.altered(workingCommit=None))

    def test_unverified_receipt_cannot_claim_verified(self) -> None:
        self.assertIn("status=verified: testResults cannot be empty",
                      self.altered(status="verified", testResults=[]))

    def test_review_requires_pr_reference(self) -> None:
        self.assertIn("status=review: pullRequests cannot be empty",
                      self.altered(pullRequests=[]))

    def test_path_traversal_rejected(self) -> None:
        self.assertTrue(self.altered(changedFiles=["../secrets.env"]))

    def test_credential_signature_rejected(self) -> None:
        fake = "ghp_" + "a" * 33
        self.assertTrue(self.altered(lastVerifiedAction=f"Do not log: {fake}"))

    def test_wrong_item_types_rejected(self) -> None:
        self.assertTrue(self.altered(changedFiles=[123]))


if __name__ == "__main__":
    unittest.main()
