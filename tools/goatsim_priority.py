"""Experimental GoatSim priority-stack scheduler.

Models *selection order*, not server execution, latency or real DPS.
No keyboard input, injection or process access.
"""
from dataclasses import dataclass
from typing import Optional

DEFAULT_DELAY_MS = 10
MAX_PRESET_DELAY_MS = 20


@dataclass(frozen=True)
class Skill:
    name: str
    cooldown_ms: int
    damage_estimate: float = 0.0
    requires: Optional[str] = None


def choose_skill(stack, now_ms, next_ready, active_conditions=()):
    """First ready/eligible skill wins. Stack is ordered highest priority first."""
    active = set(active_conditions)
    for skill in stack:
        if skill.cooldown_ms < 0:
            raise ValueError("negative cooldown")
        if skill.requires and skill.requires not in active:
            continue
        if now_ms >= next_ready.get(skill.name, 0):
            return skill
    return None


def simulate_priority(stack, duration_ms, *, delay_ms=DEFAULT_DELAY_MS, action_lock_ms=1000, conditions=()):
    """Simple deterministic scheduling baseline; damage estimates are caller-provided.

    action_lock_ms approximates casting/animation/GCD and must be calibrated.
    The 10/20ms preset represents a polling interval, NOT a claim of actual
    server tick rate or achievable DPS.
    """
    if delay_ms not in (10, 20):
        raise ValueError("supported delay presets are 10ms and 20ms")
    if duration_ms < 0 or action_lock_ms <= 0:
        raise ValueError("invalid duration or action lock")
    if len({s.name for s in stack}) != len(stack):
        raise ValueError("skill names must be unique")
    now = 0
    next_ready = {}
    busy_until = 0
    events = []
    while now < duration_ms:
        if now >= busy_until:
            skill = choose_skill(stack, now, next_ready, conditions)
            if skill is not None:
                events.append({"time_ms": now, "skill": skill.name, "estimated_damage": skill.damage_estimate})
                next_ready[skill.name] = now + skill.cooldown_ms
                busy_until = now + action_lock_ms
        now += delay_ms
    return {"events": events, "estimated_damage": sum(e["estimated_damage"] for e in events),
            "duration_ms": duration_ms, "delay_ms": delay_ms, "model": "unvalidated-priority-baseline"}
