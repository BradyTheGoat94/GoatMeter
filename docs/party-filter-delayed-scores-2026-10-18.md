# Party filter and delayed GS/CP investigation — October 18, 2026

Test branch: `test/party-gscp-fix`, based on `5f84a52` from `test/goatsim-integration`.

## Evidence

The first screenshot (02:11) shows four combat participants with GS/CP unavailable and a GROUP heading despite Party being selected. The second screenshot (02:15) shows PARTY and only the two expected players, both with scores. The matching local validation log records its first accepted 0297 roster at 02:13:35 EDT (06:13:35 UTC), with one roster member. Self is added separately. This supports delayed roster availability, not broken GS/CP overlay bindings. No private packet bytes are committed.

The decoder requires a structurally valid roster. Scores are joined by character and server IDs through explicit actor identity links, including sibling sockets. The capture bridge caches scores independently; the engine applies their freshness rules; overlay rows bind the resulting nullable values. Existing tests exercise roster-before-identity and roster-after-combat ordering.

## Confirmed defects and changes

* Unavailable Party/Force membership previously displayed all nearby combat participants. The footer warned of this, but Encounter Focus hides that footer. Restricted views now show only verified self (or no rows if self is unresolved) and put the unavailable warning in the common rate heading.
* Host party membership accumulated score-enriched identities for ten minutes. A score cache is not a current roster: old members could survive later snapshots. A typed PartyRoster event now carries the complete accepted membership snapshot through capture, independently of score metadata. The latest snapshot replaces host membership; replaying cached scores cannot add departed players back.
* The bridge retains the latest roster per scope before combat lock, publishes it when capture locks, expires it, and clears it on connection reset. Older snapshots cannot overwrite newer membership. An actor identity update without a stable link removes the host's previous actor mapping.

No score offsets, plausible-value heuristics, production branch, live installation, update feed, or public release were changed. Workflow push triggers include this test branch; public publishing remains restricted to main.

## Validation

Local checks passed: 105 xUnit protocol/combat tests; 231 legacy regression assertions; 29 Python tests; WPF offscreen overlay checks, including unavailable membership, unresolved self, verified two-player filtering, membership replacement and GS/CP row metadata. Existing package vulnerability-service connectivity warnings and xUnit analyzer warnings do not indicate test failures.

New tests cover typed roster publication, rejecting incomplete snapshots, latest-snapshot ordering, scope isolation, expiry/reset, score cache independence and roster delivery before/after combat lock across sockets.

## Remaining limits

Passive capture cannot show GS/CP before the server sends a supported roster and the exact identity join is known. This change does not force a server refresh or invent unknown values. The parser still rejects empty/unrecognized roster layouts, so complete party dissolution without a supported replacement snapshot remains subject to the existing ten-minute expiry. Leave/disband opcodes need capture evidence before implementing immediate invalidation. Force roster support remains unavailable. Completed encounter metrics and displayed share percentages retain their existing encounter-wide semantics.
