using System;
using System.Collections.Generic;
using System.Linq;

namespace GoatMeter.GoatSim;

/// <summary>
/// Explicit, fail-closed player visibility rules. Party/force membership must
/// be supplied by a verified roster, never inferred from combat proximity.
/// This service does not alter capture or encounter history.
/// </summary>
public enum PlayerViewScope { Self, Party, Force, EveryoneNearby }

public static class PlayerRosterVisibility
{
    public static IReadOnlySet<long> VisibleActors(
        PlayerViewScope scope,
        long? localActorId,
        IEnumerable<long> nearbyActors,
        IEnumerable<long>? partyActors = null,
        IEnumerable<long>? forceActors = null)
    {
        if (localActorId is null)
            throw new InvalidOperationException("Local player identity is unresolved.");

        var nearby = new HashSet<long>(nearbyActors) { localActorId.Value };
        if (scope == PlayerViewScope.Self)
            return new HashSet<long> { localActorId.Value };
        if (scope == PlayerViewScope.EveryoneNearby)
            return nearby;

        var roster = scope switch
        {
            PlayerViewScope.Party => partyActors,
            PlayerViewScope.Force => forceActors,
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        if (roster is null)
            throw new InvalidOperationException("Selected roster membership has not been verified.");

        var visible = new HashSet<long>(roster) { localActorId.Value };
        visible.IntersectWith(nearby);
        return visible;
    }

    public static bool IncludeDamage(long? sourceActorId, long? targetActorId,
        IReadOnlySet<long> visibleActors, long? selectedTargetId = null)
        => sourceActorId is long source
           && visibleActors.Contains(source)
           && (selectedTargetId is null || targetActorId == selectedTargetId);
}
