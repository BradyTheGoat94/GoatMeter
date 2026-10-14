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
