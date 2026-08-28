#!/usr/bin/env python3
"""Validate the small set of repository rules that keep Societies on one authority path."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from typing import Any
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "project-governance.json"
MARKDOWN_LINK = re.compile(r"!?\[[^\]]*\]\(([^)]+)\)")
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


def local_markdown_target(raw: str) -> str | None:
    target = raw.strip()
    if not target:
        return None
    if target.startswith("<") and ">" in target:
        target = target[1 : target.index(">")]
    else:
        target = target.split(maxsplit=1)[0]
    if target.startswith(("http://", "https://", "mailto:", "tel:", "#")):
        return None
    target = unquote(target.split("#", 1)[0]).strip()
    return target or None


def validate_markdown_links(relative: str, path: Path) -> None:
    if not path.is_file():
        return
    text = path.read_text(encoding="utf-8")
    for match in MARKDOWN_LINK.finditer(text):
        target = local_markdown_target(match.group(1))
        if target is None:
            continue
        resolved = (ROOT / target.lstrip("/")) if target.startswith("/") else (path.parent / target)
        if not resolved.resolve().exists():
            error(f"broken local Markdown link in {relative}: {target}")


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
    expected_milestone_id = "SNOW-GLOBE-SOCIAL-KERNEL-V1"
    if active_config.get("id") != expected_milestone_id:
        error(f"active milestone id must be {expected_milestone_id}")

    authorization = active_config.get("feature_work_authorization")
    expected_authorization = {
        "before_merge": False,
        "after_merge": True,
        "condition": "planning_pr_merged_to_master",
        "scope": "ordered_packets_only",
    }
    if authorization != expected_authorization:
        error(
            "active_milestone.feature_work_authorization must exactly equal "
            f"{expected_authorization!r}"
        )
    if "feature_work_authorized" in active_config:
        error("ambiguous active_milestone.feature_work_authorized boolean is prohibited")

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

    live_markdown = manifest.get("live_markdown_documents", [])
    if not isinstance(live_markdown, list) or not live_markdown:
        error("live_markdown_documents must be a non-empty array")
    else:
        for relative in live_markdown:
            if not isinstance(relative, str):
                error("live_markdown_documents entries must be strings")
                continue
            path = require_file(relative)
            validate_markdown_links(relative, path)

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
    authorization_summary = "unknown"
    if manifest:
        active = manifest.get("active_milestone", {})
        if isinstance(active, dict):
            authorization = active.get("feature_work_authorization", {})
            if isinstance(authorization, dict):
                authorization_summary = (
                    f"before_merge={str(authorization.get('before_merge')).lower()}, "
                    f"after_merge={str(authorization.get('after_merge')).lower()}, "
                    f"condition={authorization.get('condition')}, "
                    f"scope={authorization.get('scope')}"
                )
    print(
        "Project governance validation passed. "
        f"Active milestone: {milestone}. Feature authorization: {authorization_summary}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
