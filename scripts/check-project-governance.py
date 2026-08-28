#!/usr/bin/env python3
"""Validate the small set of repository rules that keep Societies on one authority path."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "project-governance.json"
ERRORS: list[str] = []


def error(message: str) -> None:
    ERRORS.append(message)


def require_file(relative: str) -> Path:
    path = ROOT / relative
    if not path.is_file():
        error(f"required file is missing: {relative}")
    return path


def load_manifest() -> dict[str, Any]:
    if not MANIFEST.is_file():
        error("project-governance.json is missing")
        return {}
    try:
        value = json.loads(MANIFEST.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        error(f"project-governance.json is unreadable or invalid: {exc}")
        return {}
    if not isinstance(value, dict):
        error("project-governance.json root must be an object")
        return {}
    return value


def main() -> int:
    manifest = load_manifest()
    if not manifest:
        return finish()

    if manifest.get("schema_version") != 1:
        error("unsupported governance schema_version; expected 1")

    if manifest.get("project") != "Societies":
        error("governance project must be Societies")

    baseline = manifest.get("baseline", {})
    accepted_commit = baseline.get("accepted_commit") if isinstance(baseline, dict) else None
    if not isinstance(accepted_commit, str) or not re.fullmatch(r"[0-9a-f]{40}", accepted_commit):
        error("baseline.accepted_commit must be a full lowercase Git SHA")
        accepted_commit = ""

    canonical = manifest.get("canonical_documents", {})
    if not isinstance(canonical, dict) or not canonical:
        error("canonical_documents must be a non-empty object")
        canonical = {}
    for role, relative in canonical.items():
        if not isinstance(relative, str) or not relative:
            error(f"canonical document path is invalid for {role}")
            continue
        require_file(relative)

    scoped = manifest.get("scoped_agent_contracts", [])
    if not isinstance(scoped, list) or not scoped:
        error("scoped_agent_contracts must be a non-empty array")
        scoped = []
    for relative in scoped:
        if isinstance(relative, str):
            require_file(relative)
        else:
            error("scoped_agent_contracts entries must be strings")

    active_config = manifest.get("active_milestone", {})
    if not isinstance(active_config, dict):
        error("active_milestone must be an object")
        active_config = {}
    if active_config.get("path") != "planning/active/MILESTONE.md":
        error("active milestone path must be planning/active/MILESTONE.md")
    if active_config.get("status") != "active":
        error("active milestone status must be active")
    if active_config.get("feature_work_authorized") is not False:
        error("CONSOLIDATION-V1 must keep feature_work_authorized false")

    active_dir = ROOT / "planning" / "active"
    if not active_dir.is_dir():
        error("planning/active directory is missing")
    else:
        allow = manifest.get("active_directory_allowlist", [])
        if not isinstance(allow, list) or not all(isinstance(item, str) for item in allow):
            error("active_directory_allowlist must be an array of names")
            allow_set: set[str] = set()
        else:
            allow_set = set(allow)
        actual = {entry.name for entry in active_dir.iterdir()}
        unexpected = sorted(actual - allow_set)
        missing = sorted(allow_set - actual)
        if unexpected:
            error("unexpected entries in planning/active: " + ", ".join(unexpected))
        if missing:
            error("allowlisted entries missing from planning/active: " + ", ".join(missing))
        extra_markdown = sorted(
            entry.name
            for entry in active_dir.glob("*.md")
            if entry.name not in {"README.md", "MILESTONE.md"}
        )
        if extra_markdown:
            error("only README.md and MILESTONE.md may be active Markdown: " + ", ".join(extra_markdown))

    max_bytes = manifest.get("root_document_max_bytes")
    root_docs = manifest.get("root_documents", [])
    if not isinstance(max_bytes, int) or max_bytes <= 0:
        error("root_document_max_bytes must be a positive integer")
    elif not isinstance(root_docs, list):
        error("root_documents must be an array")
    else:
        for relative in root_docs:
            if not isinstance(relative, str):
                error("root_documents entries must be strings")
                continue
            path = require_file(relative)
            if path.is_file() and path.stat().st_size > max_bytes:
                error(f"root authority document exceeds {max_bytes} bytes: {relative}")

    archives = manifest.get("required_archives", [])
    if not isinstance(archives, list):
        error("required_archives must be an array")
    else:
        for relative in archives:
            if isinstance(relative, str):
                require_file(relative)
            else:
                error("required_archives entries must be strings")

    if accepted_commit:
        for relative in (
            "docs/project/CURRENT_STATE.md",
            "planning/active/MILESTONE.md",
            "docs/project/DECISION_LOG.md",
        ):
            path = require_file(relative)
            if path.is_file() and accepted_commit not in path.read_text(encoding="utf-8"):
                error(f"accepted baseline SHA is not recorded in {relative}")

    current_state = ROOT / "docs" / "project" / "CURRENT_STATE.md"
    if current_state.is_file():
        text = current_state.read_text(encoding="utf-8").lower()
        required_phrases = (
            "zero participating citizens",
            "feature",
            "performance",
        )
        for phrase in required_phrases:
            if phrase not in text:
                error(f"current state must retain the bounded limitation phrase: {phrase!r}")

    return finish(manifest)


def finish(manifest: dict[str, Any] | None = None) -> int:
    if ERRORS:
        print("Project governance validation failed:", file=sys.stderr)
        for item in ERRORS:
            print(f"- {item}", file=sys.stderr)
        return 1

    milestone = "unknown"
    if manifest:
        active = manifest.get("active_milestone", {})
        if isinstance(active, dict):
            milestone = str(active.get("id", "unknown"))
    print(f"Project governance validation passed. Active milestone: {milestone}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
