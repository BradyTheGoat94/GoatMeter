# GoatMeter code review and GS/CP reliability — October 16

Scope: testing/score-identity-oct16, based on public main 2e4a269. No release/public-download changes. This is a focused patch for the desktop project; it does not replace the separate local fight-export feature branch e9222ea.

## Fixed

- The live capture filter compared adapter-prefixed scopes literally, rejecting duplicate-Npcap-adapter identity observations before the existing bridge could normalize them. Use the same normalized endpoint identity for filtering, retaining isolation between different game hosts/local addresses and retaining primary-flow-only damage dispatch.
- Accept the consistent 1001–9999 server-id range in party identities, replacing the obsolete 2021 ceiling; retain server identity through stable/session links and invalidate conflicting stable-id server shortcuts. A private October 14 capture has 95 roster frames, 93 accepted by the unchanged score parser, including server 2102. Private bytes/names are not committed.
- Persist confirmed character/server identity even before a GS/CP value exists. Previously both were gated by a score timestamp, preventing subsequent exact metadata matching.
- Apply late CharacterStats updates only to previously confirmed matching character/server/name identities. Refuse future/expired observations. No new players, fight lifecycle activity or damage; completed fight snapshots remain frozen.
- Preserve stable identity enrichment when experimental score parsing is disabled.
- Stop rebuilding bounded recent-event queues on every unchanged identity observation; refresh only changed name/class labels.
- Explain unavailable/expired score fields in overlay rows instead of only displaying dashes.

## Evidence and limits

Roster parsing remains experimental and conservative. No parser offsets or plausible-value assumptions were relaxed. Existing research/diagnostic observations are not proof that the decoded GS or CP equals the current in-game value. Need simultaneous in-game GS/CP comparison for exact server/character, both before/after party changes, before calling this live-validated. No raw diagnostic captures are included in this branch.

Validation: compiled cross-platform regression sources directly with the official .NET 8 SDK Roslyn compiler because dotnet CLI Process.GetStat is unavailable in this execution environment. 247 assertions passed, including late exact score refresh, wrong-name/server/future rejection, identity without scores, duplicate-adapter scope matching, no combat extension, and frozen completed scores. WPF Windows build and visual checks remain unrun; no desktop test windows launched.

## Recommended next work, in priority order

1. Score provenance panel: exact matched character/server, observation age, stale state and experimental label; separate roster-decode, session-link and UI-match diagnostics, making missing GS/CP actionable.
2. Expose capture integrity beside each fight: dropped queue entries and TCP gaps already have lower-level signals; freeze a per-encounter completeness indicator into history and exported summaries. Incomplete data must not silently look complete.
3. Harden history storage: bounded retention, file-size limits before deserialization, atomic save cleanup, and isolation of corrupt files. Base main FightStore currently reads whole JSON files and has no retention cap.
4. Add one export-to-archive action with a receipt reminder, retaining an explicit review of identity/privacy rather than automatic upload. Coordinate with the local e9222ea fight-export branch.
5. Profile snapshot allocation cost: category snapshot generation recursively rebuilds each category and identities refresh frequently. Optimize only after measuring a dense raid replay, preserving current/overall and frozen history semantics.

Avoid GS/CP estimates, name-only profile matching, inferred boss kills, or promoting self-reported rankings to verified. These add apparent completeness without evidence.
