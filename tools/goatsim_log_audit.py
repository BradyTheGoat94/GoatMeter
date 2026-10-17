"""Audit GoatMeter diagnostic damage logs without publishing raw player data.

Usage: python tools/goatsim_log_audit.py combat.log --actor 1425
This is a diagnostic summary, NOT the authoritative GoatMeter encounter total.
"""
import argparse
from collections import Counter, defaultdict
from datetime import datetime


def audit(lines, actor=None):
    totals = defaultdict(lambda: {"damage": 0, "events": 0, "first": None, "last": None})
    fingerprints = Counter()
    identities = {}
    for line in lines:
        parts = line.rstrip("\n").split("|")
        if len(parts) < 2:
            continue
        tag = parts[1]
        if tag not in ("tag=damage", "tag=identityMap"):
            continue
        fields = dict(piece.split("=", 1) for piece in parts[2:] if "=" in piece)
        if tag == "tag=identityMap":
            if fields.get("entity") and fields.get("name"):
                identities[fields["entity"]] = fields["name"]
            continue
        source = fields.get("src")
        try:
            amount = int(fields["amount"])
            timestamp = datetime.fromisoformat(parts[0].replace("Z", "+00:00"))
        except (KeyError, ValueError):
            continue
        if amount <= 0 or not source:
            continue
        record = totals[source]
        record["damage"] += amount
        record["events"] += 1
        record["first"] = min(record["first"], timestamp) if record["first"] else timestamp
        record["last"] = max(record["last"], timestamp) if record["last"] else timestamp
        # This is only a duplicate candidate: identical legitimate hits can exist.
        fingerprints[(source, fields.get("tgt"), fields.get("skill"),
                      amount, fields.get("raw"))] += 1
    selected = {k: v for k, v in totals.items() if actor is None or k == str(actor)}
    summary = []
    for source, item in sorted(selected.items(), key=lambda pair: -pair[1]["damage"]):
        duration = (item["last"] - item["first"]).total_seconds()
        summary.append({"actor": source, "name": identities.get(source, "unresolved"),
                        "damage": item["damage"], "events": item["events"],
                        "first": item["first"].isoformat(),
                        "last": item["last"].isoformat(),
                        "active_window_seconds": round(duration, 3),
                        "first_to_last_dps": round(item["damage"] / duration, 2) if duration > 0 else None})
    return {"actors": summary, "exact_repeat_candidates": sum(v - 1 for v in fingerprints.values()),
            "warning": "No encounter segmentation, roster validation, packet deduplication, or DoT owner verification."}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("log")
    parser.add_argument("--actor", help="Filter to one numeric actor ID")
    args = parser.parse_args()
    import json
    with open(args.log, encoding="utf-8", errors="replace") as handle:
        print(json.dumps(audit(handle, args.actor), indent=2))


if __name__ == "__main__":
    main()
