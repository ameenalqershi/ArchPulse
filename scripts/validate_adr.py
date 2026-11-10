#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Offline ADR v1 snapshot and traceability validation (T003/S22).

Does NOT authenticate GitHub reviewers, render Mermaid, or verify approval history.
"""
from __future__ import annotations

import csv
import json
import re
from datetime import date
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parent.parent
ADR_ID = re.compile(r"ADR-[0-9]{4}\Z")
TASK_ID = re.compile(r"T[0-9]{3}\Z")
SPEC_ID = re.compile(r"S[0-9]{2}\Z")
SHA = re.compile(r"[0-9a-f]{40}\Z")
REVIEW_URL = re.compile(r"https://github\.com/[^/]+/[^/]+/pull/[0-9]+#pullrequestreview-[0-9]+\Z")
DIAGRAM_HEAD = re.compile(r"^(flowchart|graph|sequenceDiagram|classDiagram|stateDiagram-v2|erDiagram|C4Context|C4Container|C4Component)(?:\s|$)")
STATUSES = {"Proposed", "Accepted", "Superseded"}
GATES = {"security": "securityRequired", "privacy": "privacyRequired",
         "contract": "contractRequired", "persistence": "persistenceRequired"}


def _url(value: object, *, domain: str | None = None) -> bool:
    if not isinstance(value, str):
        return False
    try:
        part = urlsplit(value)
    except ValueError:
        return False
    return (part.scheme == "https" and bool(part.hostname)
            and not part.username and not part.password
            and (domain is None or part.hostname == domain)
            and not any(c.isspace() for c in value))


def _date(value: object) -> bool:
    if not isinstance(value, str) or not re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
        return False
    try:
        date.fromisoformat(value)
        return True
    except ValueError:
        return False


def _nonempty(value: object, length: int = 1) -> bool:
    return isinstance(value, str) and len(value.strip()) >= length


def _dict_keys(obj: object, keys: set[str], where: str, errors: list[str]) -> dict:
    if not isinstance(obj, dict):
        errors.append(f"{where}: expected object")
        return {}
    missing = keys - obj.keys()
    extra = obj.keys() - keys
    if missing:
        errors.append(f"{where}: missing {sorted(missing)}")
    if extra:
        errors.append(f"{where}: unexpected {sorted(extra)}")
    return obj


def _strings(value: object, where: str, errors: list[str], *, minimum: int = 1,
             urls: bool = False) -> list[str]:
    if not isinstance(value, list) or len(value) < minimum or any(
        not isinstance(v, str) or not v.strip() or (urls and not _url(v)) for v in value
    ):
        errors.append(f"{where}: expected >= {minimum} nonempty {'https URLs' if urls else 'strings'}")
        return []
    if len(value) != len(set(value)):
        errors.append(f"{where}: duplicate entries")
    return value


def _review(receipt: object, where: str, errors: list[str]) -> dict:
    r = _dict_keys(receipt, {"reviewUrl", "reviewer", "commitSha", "date", "decision"},
                   where, errors)
    if not isinstance(r.get("reviewUrl"), str) or not REVIEW_URL.fullmatch(r["reviewUrl"]):
        errors.append(f"{where}: expected GitHub review URL")
    if not _nonempty(r.get("reviewer")):
        errors.append(f"{where}: missing reviewer login")
    if not isinstance(r.get("commitSha"), str) or not SHA.fullmatch(r["commitSha"]):
        errors.append(f"{where}: invalid commit SHA")
    if not _date(r.get("date")):
        errors.append(f"{where}: invalid date")
    if r.get("decision") != "Approved":
        errors.append(f"{where}: only explicit Approved review is accepted")
    return r


def _traceability(root: Path, errors: list[str]) -> dict[str, str]:
    path = root / "docs/requirements/task-registry.csv"
    try:
        with path.open(encoding="utf-8-sig", newline="") as stream:
            rows = list(csv.DictReader(stream))
        return {row["Work ID"]: row["Specification"] for row in rows}
    except (OSError, UnicodeError, csv.Error, KeyError, TypeError) as exc:
        errors.append(f"task traceability registry unavailable: {exc}")
        return {}


def _validate_one(record: object, path: Path, root: Path, tasks: dict[str, str],
                  errors: list[str]) -> dict:
    where = path.name
    expected = {"schemaVersion", "id", "title", "status", "document", "source", "author",
                "dates", "affectedModules", "alternatives", "proposal", "reviewers",
                "approval", "supersessionApproval", "supersededBy", "gates", "gateEvidence",
                "migration", "verification"}
    r = _dict_keys(record, expected, where, errors)
    rid = r.get("id")
    if not isinstance(rid, str) or not ADR_ID.fullmatch(rid) or path.name != f"{rid}.json":
        errors.append(f"{where}: ID/file mismatch or invalid ID")
    if r.get("schemaVersion") != "1.0":
        errors.append(f"{where}: unsupported schemaVersion")
    if not _nonempty(r.get("title"), 8) or not _nonempty(r.get("author"), 3):
        errors.append(f"{where}: title/author required")
    if not _nonempty(r.get("proposal"), 15):
        errors.append(f"{where}: proposal rationale required")
    if r.get("status") not in STATUSES:
        errors.append(f"{where}: invalid status")

    source = _dict_keys(r.get("source"), {"specification", "task", "notion"},
                        f"{where}.source", errors)
    spec, task = source.get("specification"), source.get("task")
    if not isinstance(spec, str) or not SPEC_ID.fullmatch(spec):
        errors.append(f"{where}: invalid specification")
    if not isinstance(task, str) or not TASK_ID.fullmatch(task) or tasks.get(task) != spec:
        errors.append(f"{where}: unknown or mismatched task/spec")
    if not _url(source.get("notion"), domain="app.notion.com"):
        errors.append(f"{where}: invalid canonical Notion URL")

    dates = _dict_keys(r.get("dates"), {"proposed", "accepted"}, f"{where}.dates", errors)
    if not _date(dates.get("proposed")):
        errors.append(f"{where}: invalid proposal date")
    if dates.get("accepted") is not None and not _date(dates["accepted"]):
        errors.append(f"{where}: invalid acceptance date")

    _strings(r.get("affectedModules"), f"{where}.affectedModules", errors)
    _strings(r.get("reviewers"), f"{where}.reviewers", errors, minimum=0)
    alternatives = r.get("alternatives")
    if not isinstance(alternatives, list) or len(alternatives) < 2:
        errors.append(f"{where}: at least two alternatives required")
        alternatives = []
    ids = []
    for i, alt in enumerate(alternatives):
        loc = f"{where}.alternatives[{i}]"
        a = _dict_keys(alt, {"id", "label", "evidence", "tradeoffs", "mermaid"}, loc, errors)
        aid = a.get("id")
        if not isinstance(aid, str) or not re.fullmatch(r"[A-Z]", aid):
            errors.append(f"{loc}: invalid alternative ID")
        else:
            ids.append(aid)
        if not _nonempty(a.get("label"), 3) or not _nonempty(a.get("tradeoffs"), 8):
            errors.append(f"{loc}: missing label or tradeoffs")
        _strings(a.get("evidence"), f"{loc}.evidence", errors, urls=True)
        diagram = a.get("mermaid")
        if (not _nonempty(diagram, 20) or not DIAGRAM_HEAD.search(diagram)
                or "%%" == str(diagram).strip() or "\x00" in str(diagram)
                or "```" in str(diagram)):
            errors.append(f"{loc}: unsupported or missing Mermaid graph")
    if len(ids) != len(set(ids)):
        errors.append(f"{where}: duplicate alternative IDs")

    document = r.get("document")
    if (not isinstance(document, str)
            or not re.fullmatch(r"docs/decisions/ADR-[0-9]{4}-[a-z0-9-]+\.md", document)
            or not isinstance(rid, str) or not document.startswith(f"docs/decisions/{rid}-")):
        errors.append(f"{where}: invalid or mismatched Markdown path")
    else:
        md_path = root / document
        try:
            md = md_path.read_text(encoding="utf-8")
        except (OSError, UnicodeError) as exc:
            errors.append(f"{where}: missing/unreadable narrative: {exc}")
            md = ""
        if rid not in md:
            errors.append(f"{where}: narrative lacks ADR ID")
        if not re.search(r"\bProposed\b|\bAccepted\b|\bSuperseded\b", md):
            errors.append(f"{where}: narrative lacks status")
        for a in alternatives:
            if not isinstance(a, dict) or not isinstance(a.get("id"), str) or not isinstance(a.get("mermaid"), str):
                continue
            if not re.search(r"(?m)^## Alternative " + re.escape(a["id"]) + r"\b", md):
                errors.append(f"{where}: alternative {a['id']} heading missing in narrative")
            if f"```mermaid\n{a['mermaid']}\n```" not in md:
                errors.append(f"{where}: alternative {a['id']} diagram differs from Markdown")

    gates = _dict_keys(r.get("gates"), set(GATES.values()), f"{where}.gates", errors)
    evidence = _dict_keys(r.get("gateEvidence"), set(GATES), f"{where}.gateEvidence", errors)
    for name, flag in GATES.items():
        if not isinstance(gates.get(flag), bool):
            errors.append(f"{where}: {flag} must be boolean")
        links = _strings(evidence.get(name), f"{where}.gateEvidence.{name}", errors,
                         minimum=0, urls=True)
        if r.get("status") != "Proposed" and gates.get(flag) is True and not links:
            errors.append(f"{where}: Accepted/Superseded requires {name} evidence")

    migration = _dict_keys(r.get("migration"), {"forward", "rollback", "compatibility"},
                           f"{where}.migration", errors)
    for key in ("forward", "rollback", "compatibility"):
        if not _nonempty(migration.get(key), 10):
            errors.append(f"{where}: missing migration.{key}")
    verification = _dict_keys(r.get("verification"), {"tests", "ciUrl", "graphImpact"},
                              f"{where}.verification", errors)
    _strings(verification.get("tests"), f"{where}.verification.tests", errors)
    if not _nonempty(verification.get("graphImpact"), 10):
        errors.append(f"{where}: graph impact must be recorded")
    ci = verification.get("ciUrl")
    if ci is not None and not _url(ci, domain="github.com"):
        errors.append(f"{where}: invalid CI link")

    status, approval, supersession = r.get("status"), r.get("approval"), r.get("supersessionApproval")
    replacement = r.get("supersededBy")
    if status == "Proposed":
        if approval is not None or supersession is not None or replacement is not None or dates.get("accepted") is not None:
            errors.append(f"{where}: Proposed cannot claim approval, acceptance or supersession")
    elif status in {"Accepted", "Superseded"}:
        receipt = _review(approval, f"{where}.approval", errors)
        reviewers = r.get("reviewers")
        if not isinstance(reviewers, list) or receipt.get("reviewer") not in reviewers:
            errors.append(f"{where}: approval reviewer must be declared")
        if str(receipt.get("reviewer", "")).casefold() == str(r.get("author", "")).casefold():
            errors.append(f"{where}: self-approval not allowed")
        if not _date(dates.get("accepted")):
            errors.append(f"{where}: missing accepted date")
        elif _date(dates.get("proposed")) and dates["accepted"] < dates["proposed"]:
            errors.append(f"{where}: acceptance precedes proposal")
        if ci is None:
            errors.append(f"{where}: Accepted/Superseded must link CI")
        if status == "Accepted":
            if replacement is not None or supersession is not None:
                errors.append(f"{where}: Accepted cannot claim supersession")
        else:
            successor_review = _review(supersession, f"{where}.supersessionApproval", errors)
            if (not isinstance(reviewers, list)
                    or successor_review.get("reviewer") not in reviewers):
                errors.append(f"{where}: supersession reviewer must be declared")
            if (successor_review.get("reviewUrl") == receipt.get("reviewUrl")
                    or successor_review.get("commitSha") == receipt.get("commitSha")):
                errors.append(f"{where}: supersession requires a distinct revision and review")
            if (_date(successor_review.get("date")) and _date(dates.get("accepted"))
                    and successor_review["date"] < dates["accepted"]):
                errors.append(f"{where}: supersession review predates acceptance")
            if str(successor_review.get("reviewer", "")).casefold() == str(r.get("author", "")).casefold():
                errors.append(f"{where}: supersession self-approval not allowed")
            if not isinstance(replacement, str) or not ADR_ID.fullmatch(replacement) or replacement == rid:
                errors.append(f"{where}: Superseded requires a different successor ADR")
    return r


def validate(root: Path = ROOT) -> list[str]:
    errors: list[str] = []
    try:
        schema = json.loads((root / "docs/decisions/adr-v1.schema.json").read_text(encoding="utf-8"))
        if schema.get("title") != "ArchPulse Architecture Decision Record v1":
            errors.append("unsupported ADR schema")
    except (OSError, UnicodeError, ValueError) as exc:
        errors.append(f"unreadable ADR JSON schema: {exc}")
    tasks = _traceability(root, errors)
    records: dict[str, dict] = {}
    directory = root / "docs/decisions/records"
    for path in sorted(directory.glob("*.json")):
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, ValueError) as exc:
            errors.append(f"{path.name}: invalid JSON: {exc}")
            continue
        record = _validate_one(raw, path, root, tasks, errors)
        rid = record.get("id")
        if isinstance(rid, str):
            if rid in records:
                errors.append(f"duplicate ADR ID {rid}")
            records[rid] = record
    if not records:
        errors.append("no machine-readable ADR records")
    for rid, record in records.items():
        if record.get("status") == "Superseded":
            target = record.get("supersededBy")
            if target not in records or records[target].get("status") != "Accepted":
                errors.append(f"{rid}: successor {target!r} is not a recorded Accepted ADR")
    return errors


if __name__ == "__main__":
    problems = validate()
    if problems:
        print("ADR VALIDATION FAILED")
        for problem in problems:
            print(f" - {problem}")
        raise SystemExit(1)
    print("ADR validation passed (offline structural checks; NOT human approval).")
