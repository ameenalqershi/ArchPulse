#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Isolated regression tests for locked NuGet package license evidence."""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from audit_nuget_licenses import audit  # noqa: E402


class NuGetLicenseAuditTests(unittest.TestCase):
    def setUp(self):
        workspace = tempfile.TemporaryDirectory(prefix="archpulse-license-")
        self.addCleanup(workspace.cleanup)
        self.root = Path(workspace.name)
        self.cache = self.root / "cache"
        self.lock = self.root / "tests" / "packages.lock.json"
        self.lock.parent.mkdir(parents=True)
        self.policy = self.root / "policy.json"
        self.policy.write_text(json.dumps({
            "acceptedSpdxExpressions": ["Apache-2.0", "MIT"],
            "pendingLegacyMetadataReviews": {}
        }), encoding="utf-8")
        self.write_lock({"Example": {"type": "Direct", "resolved": "1.0.0"}})
        self.write_nuspec("Example", "1.0.0", license_expression="MIT")

    def write_lock(self, packages):
        self.lock.write_text(json.dumps({"version": 1, "dependencies": {"net10.0": packages}}), encoding="utf-8")

    def write_nuspec(self, name, version, license_expression=None, license_url=None):
        directory = self.cache / name.lower() / version.lower()
        directory.mkdir(parents=True, exist_ok=True)
        child = f'<license type="expression">{license_expression}</license>' if license_expression else ""
        legacy = f"<licenseUrl>{license_url}</licenseUrl>" if license_url else ""
        (directory / f"{name.lower()}.nuspec").write_text(
            f"<package><metadata><id>{name}</id><version>{version}</version>{child}{legacy}</metadata></package>",
            encoding="utf-8"
        )

    def check(self, strict=False):
        return audit(self.root, self.cache, self.policy, strict)

    def test_known_spdx_is_accepted(self):
        result = self.check()
        self.assertTrue(result["passed"])
        self.assertEqual(result["packages"][0]["auditStatus"], "metadata-accepted")

    def test_unknown_spdx_fails_without_manual_review(self):
        self.write_nuspec("Example", "1.0.0", license_expression="GPL-3.0-only")
        result = self.check()
        self.assertFalse(result["passed"])
        self.assertEqual(result["packages"][0]["auditStatus"], "unreviewed")

    def test_missing_cached_nuspec_cannot_pass(self):
        self.write_lock({"NewPkg": {"type": "Direct", "resolved": "5.0.0"}})
        with self.assertRaisesRegex(ValueError, "Missing cached"):
            self.check()

    def test_declared_legacy_review_is_explicit_and_not_complete(self):
        self.write_nuspec("Example", "1.0.0", license_url="https://example.org/license")
        self.policy.write_text(json.dumps({
            "acceptedSpdxExpressions": ["MIT"],
            "pendingLegacyMetadataReviews": {
                "example@1.0.0": {"issue": "https://example.org/issues/1",
                                  "reason": "Legacy metadata requires review", "source": "https://example.org/license"}
            }
        }), encoding="utf-8")
        self.assertTrue(self.check()["passed"])
        self.assertEqual(["example@1.0.0"], self.check()["pendingManualReviews"])
        self.assertFalse(self.check(strict=True)["passed"])

    def test_unknown_new_package_fails_even_with_existing_exception(self):
        self.write_lock({"Example": {"type": "Direct", "resolved": "1.0.0"},
                         "NewPkg": {"type": "Transitive", "resolved": "2.0.0"}})
        self.write_nuspec("NewPkg", "2.0.0", license_url="https://example.org/unknown")
        self.assertFalse(self.check()["passed"])

    def test_stale_exception_fails(self):
        self.policy.write_text(json.dumps({
            "acceptedSpdxExpressions": ["MIT"],
            "pendingLegacyMetadataReviews": {
                "example@1.0.0": {"issue": "https://example.org/issues/1",
                                  "reason": "Old exception", "source": "https://example.org"}
            }
        }), encoding="utf-8")
        self.assertTrue(any("Stale" in e for e in self.check()["errors"]))

    def test_project_reference_is_not_third_party(self):
        self.write_lock({"Example": {"type": "Direct", "resolved": "1.0.0"},
                         "ArchPulse.Core": {"type": "Project"}})
        self.assertEqual(1, len(self.check()["packages"]))


if __name__ == "__main__":
    unittest.main()
