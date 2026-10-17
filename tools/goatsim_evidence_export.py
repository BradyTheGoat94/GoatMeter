"""GoatSim opt-in, privacy-minimized evidence export v1.

This module creates local JSON bytes only. It never uploads data.
"""
from __future__ import annotations

import json
import secrets
from datetime import datetime, timezone

SCHEMA = "goatsim-export-v1"
CLASSES = frozenset({"Templar", "Gladiator", "Assassin", "Ranger", "Sorcerer", "Spiritmaster", "Cleric", "Chanter"})
MAX_EVENTS = 100_000


class ExportError(ValueError):
    """Input does not satisfy the export contract."""


def _nonnegative_number(value, field):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ExportError(f"{field} must be numeric")
    if not 0 <= value < 1e12:
        raise ExportError(f"{field} is out of range")
    return value


def _utc(value):
    if not isinstance(value, str):
        raise ExportError("timestamp must be a string")
    try:
        dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ExportError("invalid ISO timestamp") from exc
    if dt.tzinfo is None:
        raise ExportError("timestamp must include timezone")
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def build_export(profile, encounters, *, consent=False):
    """Create a local privacy-filtered export; caller must display preview and request consent.

    Input event actor IDs are deliberately discarded. Only self/other roles survive.
    The resulting data cannot be reversed to original actor names or IDs.
    """
    if consent is not True:
        raise ExportError("explicit consent is required")
    if not isinstance(profile, dict) or profile.get("class") not in CLASSES:
        raise ExportError("supported class required")
    if not isinstance(encounters, list):
        raise ExportError("encounters must be a list")
    level = profile.get("level")
    if type(level) is not int or not 1 <= level <= 100:
        raise ExportError("invalid level")
    stats = profile.get("stats", {})
    if not isinstance(stats, dict) or len(stats) > 100:
        raise ExportError("invalid stats")
    clean_stats = {}
    for key, value in stats.items():
        if not isinstance(key, str) or not key.isidentifier() or len(key) > 48:
            raise ExportError("invalid stat key")
        clean_stats[key] = None if value is None else _nonnegative_number(value, key)

    clean_encounters = []
    total_events = 0
    for encounter in encounters:
        if not isinstance(encounter, dict):
            raise ExportError("invalid encounter")
        start, end = _utc(encounter.get("start")), _utc(encounter.get("end"))
        if start > end:
            raise ExportError("encounter ends before it starts")
        events = encounter.get("events")
        if not isinstance(events, list):
            raise ExportError("events must be a list")
        total_events += len(events)
        if total_events > MAX_EVENTS:
            raise ExportError("too many events")
        clean_events = []
        for event in events:
            if not isinstance(event, dict):
                raise ExportError("invalid event")
            skill = event.get("skill")
            if not isinstance(skill, str) or not 1 <= len(skill) <= 120:
                raise ExportError("invalid skill")
            role = event.get("source_role")
            if role not in ("self", "other"):
                raise ExportError("source_role must be self or other")
            clean_events.append({
                "timestamp": _utc(event.get("timestamp")),
                "skill": skill,
                "damage": _nonnegative_number(event.get("damage"), "damage"),
                "source_role": role,
                "target_role": "boss" if event.get("target_role") == "boss" else "other",
            })
        clean_encounters.append({
            "id": secrets.token_hex(12),
            "start": start,
            "end": end,
            "events": clean_events,
        })
    return {
        "schema": SCHEMA,
        "export_id": secrets.token_hex(16),
        "character": {
            "class": profile["class"],
            "level": level,
            "stats": clean_stats,
        },
        "encounters": clean_encounters,
    }


def serialize_export(export):
    return json.dumps(export, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")


def preview_export(export):
    """Preview content before a separate, explicit user-controlled save/share action."""
    return {
        "schema": export["schema"],
        "class": export["character"]["class"],
        "stat_fields": sorted(export["character"]["stats"]),
        "encounters": len(export["encounters"]),
        "events": sum(len(e["events"]) for e in export["encounters"]),
        "byte_size": len(serialize_export(export)),
    }
