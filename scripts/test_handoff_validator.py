#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Positive and adversarial handoff contract tests (S25)."""
from __future__ import annotations

import copy
import json
import tempfile
import unittest
from pathlib import Path

from validate_handoff import HANDOFF, check_file, validate_checkpoint

SCHEMA = json.loads((HANDOFF / "handoff.schema.json").read_text("utf-8"))
EXAMPLE = json.loads(
    (HANDOFF / "FOUNDATION-AUDIT-2026-11-10.json").read_text("utf-8")
)


class HandoffContractTests(unittest.TestCase):
    def base(self) -> dict:
        return copy.deepcopy(EXAMPLE)

    def check(self, changes: dict | None = None) -> list[str]:
        data = self.base()
        if changes:
            data.update(changes)
        return validate_checkpoint(data, SCHEMA)

    def assert_rejected(self, data: dict, expected: str) -> None:
        self.assertIn(expected, " | ".join(validate_checkpoint(data, SCHEMA)))

    def test_all_committed_checkpoints_parse(self) -> None:
        paths = sorted(p for p in HANDOFF.glob("*.json") if p.name != "handoff.schema.json")
        self.assertGreaterEqual(len(paths), 3)
        for path in paths:
            with self.subTest(path=path.name):
                self.assertEqual(check_file(path, SCHEMA), [])

    def test_exact_historical_pr25_checkpoint_is_readable_not_verified(self) -> None:
        path = HANDOFF / "T117-PR25-2026-11-10.json"
        historical = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual(check_file(path, SCHEMA), [])
        raw_errors = " | ".join(validate_checkpoint(historical, SCHEMA))
        self.assertIn("missing idempotencyKey", raw_errors)
        self.assertIn("invalid state", raw_errors)

    def test_historical_waiver_is_byte_pinned_and_not_reusable(self) -> None:
        path = HANDOFF / "T117-PR25-2026-11-10.json"
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / path.name
            changed = json.loads(path.read_text(encoding="utf-8"))
            changed["lastVerifiedAction"] += " (unreviewed copy)"
            candidate.write_text(json.dumps(changed, ensure_ascii=False), encoding="utf-8")
            errors = " | ".join(check_file(candidate, SCHEMA))
        self.assertIn("missing idempotencyKey", errors)
        self.assertIn("invalid state", errors)

    def test_new_unkeyed_verified_effect_is_rejected(self) -> None:
        data = self.base()
        data["externalEffects"] = [{
            "action": "publish-pr", "state": "verified", "receipt": "https://example.invalid/evidence"
        }]
        errors = " | ".join(validate_checkpoint(data, SCHEMA))
        self.assertIn("missing idempotencyKey", errors)
        self.assertIn("invalid state", errors)

    def test_valid_reference_checkpoint(self) -> None:
        self.assertEqual(self.check(), [])

    def test_missing_mandatory_property(self) -> None:
        obj = self.base()
        obj.pop("nextSafeAction")
        self.assert_rejected(obj, "missing required field: nextSafeAction")

    def test_unknown_property_is_rejected(self) -> None:
        self.assert_rejected({**self.base(), "secretExtras": {}}, "unsupported field: secretExtras")

    def test_invalid_sha_is_rejected(self) -> None:
        self.assertIn("baseCommit: expected full 40-character", " ".join(
            self.check({"baseCommit": "abcde"})))

    def test_invalid_status_and_identifiers(self) -> None:
        report = self.check({"taskId": "any", "specification": "SXYZ", "status": "done"})
        joined = " ".join(report)
        for word in ("invalid identifier", "unrecognized status"):
            self.assertIn(word, joined)

    def test_wrong_value_and_array_item_type(self) -> None:
        report = self.check({"changedFiles": [123], "workingCommit": 4})
        self.assertIn("changedFiles[0]: wrong item type", report)
        self.assertIn("workingCommit: wrong value type", report)

    def test_no_secrets_in_checkpoint(self) -> None:
        report = self.check({"lastVerifiedAction": "example ghp_" + "A" * 30})
        self.assertIn("possible credential/private key signature in checkpoint", report)

    def test_sensitive_named_field_is_rejected(self) -> None:
        report = self.check({"externalEffects": [{
            "action": "create-pr", "state": "planned", "receipt": None,
            "idempotencyKey": "test-one", "access_token": "redacted"
        }]})
        self.assertIn("forbidden sensitive field: access_token", report)

    def test_unknown_external_effect_must_reconcile(self) -> None:
        report = self.check({"externalEffects": [{
            "action": "create-pr", "state": "unknown", "receipt": None,
            "idempotencyKey": "effect-one"
        }], "nextSafeAction": "Create the PR again"})
        self.assertTrue(any("reconciliation" in err for err in report))

    def test_unknown_with_reconciliation_is_valid(self) -> None:
        report = self.check({"externalEffects": [{
            "action": "create-pr", "state": "unknown", "receipt": None,
            "idempotencyKey": "effect-one"
        }], "nextSafeAction": "Inspect and reconcile GitHub PR list before retry"})
        self.assertEqual(report, [])

    def test_success_effect_requires_receipt(self) -> None:
        report = self.check({"externalEffects": [{
            "action": "create-pr", "state": "succeeded", "receipt": None,
            "idempotencyKey": "effect-one"
        }]})
        self.assertTrue(any("success needs a receipt" in err for err in report))

    def test_duplicate_effect_idempotency_key_rejected(self) -> None:
        a = {"action": "create-pr", "state": "planned",
             "receipt": None, "idempotencyKey": "same"}
        report = self.check({"externalEffects": [a, dict(a)]})
        self.assertTrue(any("duplicate idempotencyKey" in err for err in report))

    def test_verified_cannot_have_no_test_evidence(self) -> None:
        report = self.check({"status": "verified", "workingCommit": None, "testResults": []})
        self.assertTrue(any("verified checkpoint requires" in err for err in report))

    def test_damaged_json_file_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            bad = Path(temp) / "checkpoint.json"
            bad.write_text("{")
            report = check_file(bad, SCHEMA)
        self.assertTrue(any("invalid checkpoint" in err for err in report))

    def test_untrusted_repository_url_is_rejected(self) -> None:
        self.assertTrue(any("repository:" in err for err in
                            self.check({"repository": "https://evil.example/repo"})))


if __name__ == "__main__":
    unittest.main()
