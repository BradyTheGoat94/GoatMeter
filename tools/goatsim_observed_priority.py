"""Connect opt-in character snapshots to the GoatSim priority baseline.

All numbers are *observed per-hit estimates*, not inferred AION 2 coefficients.
"""
from __future__ import annotations

from collections import defaultdict
from statistics import median

try:
    from tools.goatsim_priority import Skill, simulate_priority
except ModuleNotFoundError:
    from goatsim_priority import Skill, simulate_priority


def observed_skill_medians(events, *, source_role="self"):
    """Return per-skill median observed damage; excludes ambiguous or non-direct hits."""
    buckets = defaultdict(list)
    for event in events:
        if not isinstance(event, dict):
            continue
        if event.get("source_role") != source_role:
            continue
        if event.get("damage_kind") != "direct":
            continue
        skill, damage = event.get("skill"), event.get("damage")
        if not isinstance(skill, str) or not skill:
            continue
        if isinstance(damage, bool) or not isinstance(damage, (int, float)) or not 0 <= damage < 1e12:
            continue
        buckets[skill].append(damage)
    return {skill: {"median_damage": median(values), "sample_count": len(values)}
            for skill, values in sorted(buckets.items())}


def simulate_from_observations(priority, observed_events, *, duration_ms=60000,
                               delay_ms=10, action_lock_ms=1000):
    """Simulate supplied priority rules using observed direct-hit medians.

    priority: ordered dicts with name, cooldown_ms and optional requires.
    Missing evidence fails closed instead of inventing skill damage.
    """
    observed = observed_skill_medians(observed_events)
    stack = []
    for item in priority:
        name = item["name"]
        if name not in observed:
            raise ValueError(f"no eligible observed direct-hit evidence for {name}")
        stack.append(Skill(name=name, cooldown_ms=item["cooldown_ms"],
                           damage_estimate=observed[name]["median_damage"],
                           requires=item.get("requires")))
    result = simulate_priority(stack, duration_ms, delay_ms=delay_ms,
                               action_lock_ms=action_lock_ms)
    result["calibration"] = {
        "basis": "observed direct-hit medians; unvalidated scheduling",
        "skill_samples": {name: observed[name]["sample_count"] for name in [s.name for s in stack]},
        "warning": "Not validated against held-out encounters. Not an accurate DPS forecast.",
    }
    return result
