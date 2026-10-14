"""Audit a downloaded A2 Tools report; never imports scores into GoatMeter."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path

MAX_BYTES = 5 * 1024 * 1024

def integer(value, label, low, high):
    if type(value) is not int or not low <= value <= high:
        raise ValueError(f"{label}: expected integer in {low}..{high}")
    return value

def audit(path, source_url):
    path = Path(path)
    if path.stat().st_size > MAX_BYTES:
        raise ValueError("report exceeds 5 MiB")
    raw = path.read_bytes()
    if len(raw) > MAX_BYTES:
        raise ValueError("report exceeds 5 MiB")
    data = json.loads(raw)
    if not isinstance(data, dict) or not isinstance(data.get("record"), dict):
        raise ValueError("expected A2 Tools record object")
    record = data["record"]
    actors = record.get("actors")
    if not isinstance(actors, list) or not 1 <= len(actors) <= 100:
        raise ValueError("expected 1..100 actors")
    start = integer(record.get("startTimeMs"), "startTimeMs", 1, 4102444800000)
    capture = dt.datetime.fromtimestamp(start / 1000, dt.timezone.utc).isoformat()
    rows = []
    seen = set()
    for actor in actors:
        if not isinstance(actor, dict):
            raise ValueError("actor must be an object")
        actor_id = integer(actor.get("actorId"), "actorId", 1, 2**63-1)
        server = integer(actor.get("serverId"), "serverId", 1001, 9999)
        if (actor_id, server) in seen:
            raise ValueError("duplicate actor/server in report")
        seen.add((actor_id, server))
        name = actor.get("nickname")
        if not isinstance(name, str) or not 1 <= len(name) <= 100:
            raise ValueError("invalid nickname")
        masked = "*" in name
        gs = actor.get("gearScore")
        cp = actor.get("combatPower")
        if gs is not None:
            gs = integer(gs, "gearScore", 0, 50000) or None
        if cp is not None:
            cp = integer(cp, "combatPower", 0, 10000000) or None
        rows.append({"name": name, "masked": masked, "server_id": server,
                     "report_actor_id": actor_id, "gear_score": gs, "combat_power": cp,
                     "stable_character_id": None, "eligible_for_live_identity_join": False})
    return {"source_url": source_url, "sha256": hashlib.sha256(raw).hexdigest(),
            "fight_started_utc": capture, "parser_version": data.get("parserVersion"),
            "classification": "historical_public_report",
            "raw_roster_packet_present": False, "decoder_independently_verified": False,
            "live_import_allowed": False,
            "limitation": "Report actor IDs are session IDs, not stable character IDs. "
                          "Report values do not verify wire offsets or current gear.",
            "players": rows}

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--source-url", required=True, help="Public report URL for provenance; no request is made")
    args = parser.parse_args()
    try:
        print(json.dumps(audit(args.report, args.source_url), indent=2, ensure_ascii=False))
    except (ValueError, OSError, OverflowError) as error:
        parser.exit(2, f"Audit failed: {error}\n")
