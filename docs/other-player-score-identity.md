# Other-player score identity link

The October 17 private capture exposed the difference between decoding roster
scores and attaching them to actual combat rows. The old path required a
separate `20 36` session link; logged links covered only one of the ten roster
characters. More decoded roster records alone did not fix the missing scores.

Independent inspection of `45 36` player-info records found a full dbid before
a bounded attribute list. The dbid contains a 48-bit character number and
16-bit server number. In the observed layout the character number fits u32.
The attribute list has either four or five fixed-size entries, with sorted
IDs `CD, CE, D0, 227` or `CD, CE, D0, D6, 227`. Each has a u32 value; the list
is followed by a level in 1–60 and a zero u32. The implementation requires
exactly one complete matching block after the validated character name.
Bare plausible integers or ambiguous blocks are rejected.

All 130 detailed player-info observations associated with eight known roster
characters carried the exact same full dbid as their roster rows. Duplicate
diagnostic records account for the previously reported 260 observations.
Of 678 detailed player-info records overall, 675 matched this structure,
with no ambiguous matches. These are observed capture facts, not proof that
all possible client layouts are supported.

The dispatcher now emits stable character/server metadata for the packet's
explicit combat entity and joins scores on exact character, server and name.
It handles either arrival order, expires stale links, invalidates links on a
new player-info observation, and rejects conflicts with a separate explicit
session link. Existing visibility-removal retention and endpoint
isolation in the capture bridge is preserved. A score-enriched PlayerName also
supplies the verified roster observation to the host's Party filter, because
raw CharacterStats are deliberately retained inside the capture bridge.

A chronological diagnostic replay of the sampled identity and roster records
attached scores to eight distinct roster characters. This flattened replay
does not reconstruct endpoint streams and is not evidence of live UI results.
Separate integration tests inject roster, identity and damage through different
capture sockets and verify GS/CP on the resulting combat snapshot, with roster
data arriving both before and after combat lock. Damage totals stay unchanged.

The 23 newer roster samples rejected for inconsistent server fields remain
rejected. This change does not relax that independent validation rule. Current
in-game value comparison and party-leave semantics still need live validation.
No private names, dbids or raw log bytes are committed.
