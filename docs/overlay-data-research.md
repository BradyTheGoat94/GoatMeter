# Overlay data research — 2026-10-14

The Encounter Focus preset is implemented separately from Modern Glass. It uses existing combat snapshots. A separate event countdown window now supports source-labeled community rift schedule estimates and import; confirmed live spawn alerts and GS/CP remain pending.

## Public projects inspected

- AionFlex: https://aionflex.gg and /accuracy. Public product/accuracy pages inspected. No official public source repository was confirmed; the similarly named GitHub profile is not proof of a relationship.
- cyberbadger6969/aion2-dps-meter, commit 414de5c1d5f3d45ce35701bd25229c2dba736bb6: GPL-3.0. Source describes party-roster GS/CP, self CP, field-boss map lists and per-server timer tracking. No implementation code copied.
- SkeeveAN/Aion-DPS-Meter, commit 2b903a40b27fe50163d182a6aaf944c1f1cf717b: MIT. Source describes inspected-character gear score, event recurrence evaluation and field-boss data. No implementation code copied.
- jeojabon/JaMeter: the inspected public repository contains README/screenshots, not the application implementation.

## Candidate inputs and validation

| Feature | Candidate input | Required verification |
| --- | --- | --- |
| Party GS and CP | Party roster, candidate opcode bytes 02 97 | Join/reform party and compare every displayed score with the in-game roster; correlate by server and stable identity, never name alone. Distinguish GS from CP. |
| Field boss countdown | Map field-boss list, candidate opcode bytes 01 91 | Open the boss list on the in-game map; match map, slot, alive state and timestamp to the UI, including an updated list. Scope cache to server/channel when supplied. |
| Spawn alert | Confirmed alive/spawn information or validated next-spawn time | A scheduled due time is only “spawn due”; reserve “spawned” for observed game evidence. Do not claim knowledge of unseen channels. |
| Event countdown | Verified Global schedule plus server timezone and entry window | Compare with the current game schedule, including midnight, DST and maintenance. Community timetable discovery is not independent verification. |
| Character GS fallback | Character inspection or public character profile | Validate exact region/server/character identity, timestamp, stale-cache handling and the metric returned. No credentials or gameplay requests. |

GoatMeter now records up to 12 `featureCandidate` samples per dispatcher for the two candidate opcode families, up to 32 KiB per frame. Oversized frames omit raw bytes. Samples are labeled unverified and never alter combat totals or create spawn/score events. Existing logging stays local.

For the next validation session: use this testing build; join/reform a party, open the in-game field-boss list, open your character sheet and inspect a party member. Retain the combat validation log and screenshots of the displayed scores/countdowns with timestamps. Confirm the Global region/server; user-local time alone does not establish server schedule time.

## Rift imports

Public reports currently conflict. Independently represented schedule facts (no external application code or schedule asset copied) are bundled as three distinct estimates: Global UTC 00:00 +3h (problem-xyz schedule snapshot 4c8d0eb, updated October 12), UTC+9 00:00 +3h (Metabot client-data report), and UTC+9 02:00 +3h (aion2rifttimer community report). Each has a 10-minute estimated entry window. Shugo hourly reminders use a three-minute estimated signup window as in the user's reference; this is editable and is not confirmed server state. All countdowns are visibly labeled estimates. No remote fetch, schedule execution, uploads or automatic game input is added. Imports accept only bounded JSON with named sources, timezones and valid daily times.

A public Altgard field-boss-list capture dated October 6 is present in cyberbadger6969's ProtocolTests.cs. This can validate field layouts offline; it cannot establish today's server state. Their GearTests.cs fixtures are synthetic constructions of the reported roster layout, not independent current gameplay capture.

## GS/CP testing integration

An independent strict roster reader is now implemented, behind ExperimentalRosterScores (enabled only in the testing profile). It accepts complete, bounded member records with an encoded character/server dbid, valid class/level/GS fields, a unique repeated-server anchor and bounded CP. It does not emit score metadata for a combat actor unless an explicit 20 36 session/stable identity link and matching name resolve uniquely; known self-server identity further restricts candidates. No external implementation copied.

Metadata passes through identity-only sibling sockets, the capture adapter, the engine, category snapshots, stored history, all overlay styles and reports. Stale live values expire; completed fight metadata is frozen; renamed/reused actor identities cannot carry scores forward. The available older user logs establish party identity traffic but did not provide complete score-roster evidence in the inspected ranges. Synthetic layout fixtures validate the new reader; current Global in-game value comparison is still pending.

## Recent public releases reviewed

- cyberbadger6969 0.2.7 (October 11) adds compact rows, inline GS/CP and row scaling; 0.2.8 (October 12) adds boss/dungeon deadlines received from the game. GoatMeter adds independent style/scale options; received deadlines remain pending a suitable validated packet source.
- SkeeveAN 0.45.7 (October 14) improves instance/session grouping. GoatMeter retains accurate per-fight segmentation rather than inferring dungeon victory from teleports.
- Iota-Nine Eclipse 3.0.19 (October 14) improves exact server separation for field-boss lists; its 3.0.18 adds alarm volume. GoatMeter strengthens exact endpoint/character/server metadata sharing.
- Public meters also provide summary copying and stream privacy. GoatMeter adds an independent clipboard-only formatter and display freeze that preserves capture, plus persisted UI scaling. No external application code copied.

CharacterStats records are internal metadata only; the capture adapter never feeds them to combat aggregation. Score metadata from sibling/duplicate adapter observations is joined only on exact endpoint scope, stable character, known server and matching observed name; unknown server stays unknown.

## Public GS/CP report audit — 2026-10-14

Downloaded and inspected the JSON export of:
https://a2tools.app/logs/sOfPWkE1vJ5bH4LsUkUREw

Report SHA-256: `7f8075db1a0347b4980a97f0c9237305df242f370793a2b7a176de927d1f4b99`.
Fight started **2026-10-10 19:09:53.080 UTC**; parser version **2.0.55.2**.
The export contains structured `gearScore`, `combatPower`, `serverId` and
`actorId` fields in `record.actors`. These are public report values, not
independently verified current character-sheet values.

| Report row | Server ID | GS | CP | Identity available |
| --- | --- | --- | --- | --- |
| Ranger (masked) | 2201 | 2157 | 108944 | Session actor only |
| Cleric (masked) | 2201 | 2177 | 102203 | Session actor only |
| Assassin (masked) | 2202 | 2261 | 103622 | Session actor only |
| Spiritmaster (public name) | 2201 | 2115 | 113952 | Session actor only |

The page lists this as an NAW fight. The party includes a member from a
second server: the top-level report server must never overwrite individual
member server IDs. Three names are masked; all four rows lack stable
character IDs. No raw roster packet is included in this inspected export.
Therefore this report proves public GS/CP reference data is obtainable,
but does **not** prove GoatMeter's byte offsets, stable identity linkage,
score observation time, or current live values. The fight timestamp is not
necessarily the time at which its scores were sampled.

Reviewed the published roster decoder in
`cyberbadger6969/aion2-dps-meter`,
`src/AionMeter.Core/Protocol/PacketParser.cs`, blob
`b0ad45d6fe4bf12725c40ebb52caeb8f280b629e`.
Its reported `02 97` layout agrees with the existing experimental roster
reader. This is corroborating implementation evidence; its synthetic gear
fixtures are not a substitute for a raw capture plus an in-game comparison.
Its self-CP reader searches for plausible integers in a variable tail;
that heuristic is not added to GoatMeter without structural validation.

Shugo.GG's own FAQ (https://shugo.gg/faq) says character profiles are fetched
from the official API with a short cache, Global is supported, and there is
no public Shugo API. No successful direct official Global character endpoint
or same-character score cross-check was established in this audit.
Abyss Logs documents public fights and GS/CP leaderboards; an individual
export was not obtained here. Do not label either route integrated or verified.

### Repeatable offline audit

Run `python tools/audit_public_scores.py downloaded-fight.json --source-url PUBLIC_REPORT_URL`.
The tool makes no network request, checks input bounds, rejects malformed or
duplicate actor/server rows, preserves per-member servers, records the file
hash and fight time, and labels every row historical and ineligible for a
live identity join. It does not feed metadata or damage into the meter.

Run `python -m unittest discover -s tools -p 'test_*.py' -v`.
Five tests cover historical identity rejection, masked/unknown scores,
invalid numeric fields, duplicate identities and oversized files. All passed
on this audit. No C# application code changed; a new Windows build was not
run locally (the .NET SDK is unavailable in this environment).

Remaining acceptance gate: current Global roster bytes plus a timestamped
in-game roster/character-sheet comparison, including a score change and two
same-named characters on different servers. Retain the existing experimental
flag and exact character/server binding until that gate passes.
