#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Offline NuGet license metadata audit. Not a legal opinion or release certification."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def package_records(repo: Path):
    found = {}
    lockfiles = sorted(p for p in repo.rglob("packages.lock.json")
                       if not any(part in {"bin", "obj", ".git"} for part in p.relative_to(repo).parts))
    if not lockfiles:
        raise ValueError("No NuGet packages.lock.json found")
    for lockfile in lockfiles:
        document = json.loads(lockfile.read_text(encoding="utf-8"))
        for target, dependencies in document.get("dependencies", {}).items():
            for name, metadata in dependencies.items():
                if metadata.get("type") == "Project":
                    continue
                version = metadata.get("resolved")
                if not version or "/" in name or "\\" in name or "/" in version or "\\" in version:
                    raise ValueError(f"Invalid resolved NuGet identity: {lockfile}: {name}@{version}")
                key = (name.lower(), version.lower())
                entry = found.setdefault(key, {"package": name, "version": version, "usedBy": []})
                entry["usedBy"].append(f"{lockfile.relative_to(repo)}:{target}:{metadata.get('type', 'Unknown')}")
    return list(found.values())


def license_metadata(cache: Path, package: str, version: str):
    spec = cache / package.lower() / version.lower() / f"{package.lower()}.nuspec"
    if not spec.is_file():
        raise ValueError(f"Missing cached .nuspec for {package}@{version}; run dotnet restore --locked-mode")
    metadata = next((e for e in ET.parse(spec).getroot().iter()
                     if e.tag.rsplit("}", 1)[-1] == "metadata"), None)
    if metadata is None:
        raise ValueError(f"Missing NuGet metadata: {package}@{version}")
    props = {e.tag.rsplit("}", 1)[-1]: e for e in metadata}
    license_element = props.get("license")
    expression = (license_element.text or "").strip() if license_element is not None else None
    kind = license_element.attrib.get("type") if license_element is not None else None
    legacy = props.get("licenseUrl")
    return {"licenseExpression": expression if kind == "expression" else None,
            "licenseFile": expression if kind == "file" else None,
            "legacyLicenseUrl": (legacy.text or "").strip() if legacy is not None else None}


def audit(repo: Path, cache: Path, policy_file: Path, require_complete: bool = False):
    policy = json.loads(policy_file.read_text(encoding="utf-8"))
    accepted = set(policy["acceptedSpdxExpressions"])
    exceptions = policy["pendingLegacyMetadataReviews"]
    packages = package_records(repo)
    failures = []
    observed_exceptions = set()
    for entry in packages:
        identity = f"{entry['package'].lower()}@{entry['version'].lower()}"
        entry.update(license_metadata(cache, entry["package"], entry["version"]))
        expression = entry["licenseExpression"]
        if expression in accepted:
            entry["auditStatus"] = "metadata-accepted"
        elif identity in exceptions:
            review = exceptions[identity]
            if not all(review.get(k) for k in ("issue", "reason", "source")):
                failures.append(f"Malformed pending review for {identity}")
            # An exception is only for unchanged URL-only legacy metadata.
            # Never let it mask a newly declared SPDX expression or license file.
            if expression is not None or entry["licenseFile"] is not None:
                failures.append(f"Legacy URL-only exception cannot override a declared license: {identity}")
            legacy_url = entry["legacyLicenseUrl"]
            if not legacy_url or legacy_url != review.get("source"):
                failures.append(f"Legacy license URL absent or changed for {identity}")
            entry["auditStatus"] = "manual-review-pending"
            entry["reviewIssue"] = review.get("issue")
            observed_exceptions.add(identity)
            if require_complete:
                failures.append(f"Unresolved manual review: {identity}")
        else:
            entry["auditStatus"] = "unreviewed"
            failures.append(f"New or unknown license metadata: {identity} ({expression or entry['legacyLicenseUrl'] or 'none'})")
    unused = set(exceptions) - observed_exceptions
    for identity in sorted(unused):
        failures.append(f"Stale exception (no matching unresolved package): {identity}")
    return {"schema": "archpulse-nuget-license-audit/v1",
            "scope": "Restored NuGet dependency metadata only; not bundled assets, toolchain or runtime licensing",
            "packages": packages, "pendingManualReviews": sorted(observed_exceptions),
            "errors": failures, "passed": not failures}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parent.parent)
    ap.add_argument("--packages", type=Path, default=Path(os.environ.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))))
    ap.add_argument("--policy", type=Path)
    ap.add_argument("--require-complete", action="store_true")
    ap.add_argument("--output", type=Path, help="Write a machine-readable local evidence report")
    args = ap.parse_args(argv)
    try:
        result = audit(args.repo, args.packages, args.policy or args.repo / "docs/operations/nuget-license-policy.json", args.require_complete)
    except (OSError, ValueError, ET.ParseError, json.JSONDecodeError) as error:
        print(f"NUGET LICENSE AUDIT ERROR: {error}", file=sys.stderr)
        return 2
    for pkg in result["packages"]:
        print(f"{pkg['auditStatus']}: {pkg['package']}@{pkg['version']} — {pkg['licenseExpression'] or pkg['legacyLicenseUrl'] or 'NO METADATA'}")
    print(f"Packages: {len(result['packages'])}; pending review: {len(result['pendingManualReviews'])}; failures: {len(result['errors'])}")
    for error in result["errors"]:
        print(f"ERROR: {error}", file=sys.stderr)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
