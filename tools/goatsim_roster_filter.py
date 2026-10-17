"""Pure roster visibility policy for experimental GoatMeter/GoatSim.

Never infer party or force membership from proximity. This is a Python
reference model until equivalent logic is wired into the C# meter.
"""
from enum import Enum


class Scope(str, Enum):
    SELF = "self"
    PARTY = "party"
    FORCE = "force"
    NEARBY = "nearby"


class UnknownRosterError(ValueError):
    pass


def visible_actor_ids(*, scope, self_id, nearby_ids, party_ids=None, force_ids=None):
    """Return visible actor IDs; None roster means unavailable, not empty.

    Membership IDs are session-local. Force membership can be independent
    of party; do not silently merge the two rosters.
    """
    scope = Scope(scope)
    nearby = set(nearby_ids)
    if self_id is None:
        raise ValueError("local player identity is required")
    if scope is Scope.SELF:
        return {self_id}
    if scope is Scope.NEARBY:
        return nearby | {self_id}
    if scope is Scope.PARTY:
        if party_ids is None:
            raise UnknownRosterError("party membership not verified")
        return (set(party_ids) | {self_id}) & (nearby | {self_id})
    if force_ids is None:
        raise UnknownRosterError("force membership not verified")
    return (set(force_ids) | {self_id}) & (nearby | {self_id})


def filtered_events(events, *, scope, self_id, nearby_ids, party_ids=None,
                    force_ids=None, target_id=None):
    """Filter by damage SOURCE, optionally by target, without mutating events.

    Every event must have explicit source_id; ambiguous events are excluded.
    """
    allowed = visible_actor_ids(scope=scope, self_id=self_id,
                                nearby_ids=nearby_ids, party_ids=party_ids,
                                force_ids=force_ids)
    return [event for event in events
            if event.get("source_id") in allowed
            and (target_id is None or event.get("target_id") == target_id)]
