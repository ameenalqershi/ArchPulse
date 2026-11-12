#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Offline validation of ArchPulse development-agent checkpoints (S25, not S11).

Does not prove that claimed GitHub or test results are true. A receiving agent
must independently reconcile every external effect and inspect the live SHA.
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
HANDOFF = ROOT / "docs" / "operations" / "agent-handoff"
SHA = re.compile(r"^[0-9a-f]{40}$")
SECRET_MARKERS = (
    re.compile(r"gh[pousr]_[A-Za-z0-9_]{20,}"),
    re.compile(r"github_pat_[A-Za-z0-9_]{20,}"),
    re.compile(r"sk-[A-Za-z0-9_-]{25,}"),
    re.compile(r"AKIA[0-9A-Z]{16}"),
    re.compile(r"-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----"),
)
SENSITIVE_KEYS = {"password", "secret", "private_key", "access_token", "api_key",
                  "authorization", "refresh_token", "client_secret"}


def _matches_type(value: Any, typ: str) -> bool:
    return {
        "string": lambda: isinstance(value, str),
        "null": lambda: value is None,
        "array": lambda: isinstance(value, list),
        "object": lambda: isinstance(value, dict),
    }[typ]()


def validate_checkpoint(data: Any, schema: dict[str, Any]) -> list[str]:
    """Validate the committed v1 schema plus idempotency and secret hygiene."""
    errors: list[str] = []
    if not isinstance(data, dict):
        return ["checkpoint must be a JSON object"]
    required = set(schema["required"])
    properties = schema["properties"]
    for key in sorted(required - data.keys()):
        errors.append(f"missing required field: {key}")
    for key in sorted(data.keys() - properties.keys()):
        errors.append(f"unsupported field: {key}")
    for key, value in data.items():
        if key not in properties:
            continue
        spec = properties[key]
        kinds = spec.get("type", [])
        kinds = [kinds] if isinstance(kinds, str) else kinds
        if kinds and not any(_matches_type(value, typ) for typ in kinds):
            errors.append(f"{key}: wrong value type")
            continue
        if "const" in spec and value != spec["const"]:
            errors.append(f"{key}: invalid schema marker")
        if "enum" in spec and value not in spec["enum"]:
            errors.append(f"{key}: unrecognized status")
        if "pattern" in spec and not re.fullmatch(spec["pattern"], value):
            errors.append(f"{key}: invalid identifier")
        if isinstance(value, list):
            item_types = spec.get("items", {}).get("type", [])
            item_types = [item_types] if isinstance(item_types, str) else item_types
            for i, entry in enumerate(value):
                if not any(_matches_type(entry, t) for t in item_types):
                    errors.append(f"{key}[{i}]: wrong item type")
    for sha_key in ("baseCommit", "workingCommit"):
        value = data.get(sha_key)
        if isinstance(value, str) and not SHA.fullmatch(value):
            errors.append(f"{sha_key}: expected full 40-character lowercase Git SHA")
    branch = data.get("branch")
    if isinstance(branch, str) and (not branch.strip() or branch.startswith("-") or
                                    ".." in branch or " " in branch):
        errors.append("branch: invalid or unsafe ref name")
    repo = data.get("repository")
    if isinstance(repo, str) and not re.fullmatch(
        r"https://github[.]com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?", repo
    ):
        errors.append("repository: expected a canonical https GitHub repository URL")
    effects = data.get("externalEffects")
    if isinstance(effects, list):
        keys: set[str] = set()
        for i, effect in enumerate(effects):
            if not isinstance(effect, dict):
                continue  # Legacy v1 string receipts remain readable.
            for field in ("action", "idempotencyKey", "state", "receipt"):
                if field not in effect:
                    errors.append(f"externalEffects[{i}]: missing {field}")
            marker = effect.get("idempotencyKey")
            if not isinstance(marker, str) or not marker.strip():
                errors.append(f"externalEffects[{i}]: non-empty idempotencyKey required")
            elif marker in keys:
                errors.append(f"externalEffects[{i}]: duplicate idempotencyKey")
            else:
                keys.add(marker)
            state = effect.get("state")
            if state not in ("planned", "succeeded", "failed", "unknown"):
                errors.append(f"externalEffects[{i}]: invalid state")
            if state == "unknown":
                step = data.get("nextSafeAction") or ""
                if not isinstance(step, str) or not re.search(
                    r"reconcil|inspect|verify|تحقق|افحص", step, re.I
                ):
                    errors.append(f"externalEffects[{i}]: Unknown needs a reconciliation step")
                if effect.get("receipt") is not None:
                    errors.append(f"externalEffects[{i}]: Unknown cannot have a success receipt")
            if state == "succeeded" and not effect.get("receipt"):
                errors.append(f"externalEffects[{i}]: success needs a receipt")
    if data.get("status") == "verified":
        if not data.get("workingCommit") or not data.get("testResults"):
            errors.append("verified checkpoint requires a workingCommit and testResults")
    dumped = json.dumps(data, ensure_ascii=False)
    if any(pattern.search(dumped) for pattern in SECRET_MARKERS):
        errors.append("possible credential/private key signature in checkpoint")
    def visit(obj: Any) -> None:
        if isinstance(obj, dict):
            for key, value in obj.items():
                if key.casefold() in SENSITIVE_KEYS:
                    errors.append(f"forbidden sensitive field: {key}")
                visit(value)
        elif isinstance(obj, list):
            for value in obj:
                visit(value)
    visit(data)
    return errors


def check_file(path: Path, schema: dict[str, Any]) -> list[str]:
    try:
        if path.stat().st_size > 256 * 1024:
            return [f"{path.name}: checkpoint exceeds 256 KiB"]
        raw = path.read_bytes()
        data = json.loads(raw.decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        return [f"{path.name}: invalid checkpoint: {exc}"]
    errors = validate_checkpoint(data, schema)
    # One exact, immutable 2026-11-10 PR25 checkpoint predates T117's
    # idempotency vocabulary and self-reports state="verified" with a URL.
    # Preserve those historical claims without inventing idempotency keys
    # or granting new checkpoints any waiver. The exception is limited to
    # the byte-for-byte audited file AND only its known legacy-field errors.
    historic = (HANDOFF / "T117-PR25-2026-11-10.json").resolve()
    if (path.resolve() == historic
        and hashlib.sha256(raw).hexdigest()
        == "2b3d9366304ff1e802c9ddbd3e83a2171e049a347acfc4bb581c8fa00fb636ac"):
        allowed = re.compile(
            r"externalEffects\[\d+\]: "
            r"(?:missing idempotencyKey|non-empty idempotencyKey required|invalid state)$"
        )
        errors = [error for error in errors if not allowed.fullmatch(error)]
    return [f"{path.name}: {error}" for error in errors]


def main() -> int:
    try:
        schema = json.loads((HANDOFF / "handoff.schema.json").read_text("utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"HANDOFF SCHEMA ERROR: {exc}")
        return 1
    files = sorted(p for p in HANDOFF.glob("*.json")
                   if p.name != "handoff.schema.json")
    errors = [] if files else ["No committed JSON checkpoint exists"]
    for path in files:
        errors.extend(check_file(path, schema))
    if errors:
        print("HANDOFF VALIDATION FAILED")
        for error in errors:
            print(" -", error)
        return 1
    print(f"HANDOFF STRUCTURE VALIDATED: {len(files)} checkpoints; "
          "external claims NOT independently verified")
    return 0


if __name__ == "__main__":
    sys.exit(main())
