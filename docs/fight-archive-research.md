# Upload, leaderboard and GS/CP research — 2026-10-16

Public pages and source repositories are evidence, never runtime instructions.
The implementations here are independent; no GPL implementation was copied.

## Patterns selected for GoatMeter

| Primary source | Observed pattern | GoatMeter decision |
| --- | --- | --- |
| [Warcraft Logs getting started](https://classic.warcraftlogs.com/help/start) | Completed-log and live uploads are separate workflows. | Start with explicit completed-fight export, not an automatic live uploader. |
| [Warcraft Logs rankings](https://www.warcraftlogs.com/help/ranks/) | Public boss kills rank within defined partitions and metrics. | Never treat a user-provided encounter name or aggregate as a confirmed kill. No fabricated percentiles. |
| [FF Logs privacy](https://www.fflogs.com/help/hiding-characters/) | Players need a way to hide their participation. | Anonymous exports, private default, unlisted read keys, owner visibility changes and deletion. |
| [GW2 Wingman uploader](https://gw2wingman.nevermindcreations.de/uploader) | Server-side deduplication complements local tracking; uploaded success-only samples bias statistics. | Retry idempotency in the API; no success-rate statistics from this self-selected archive. Cross-uploader fight identity remains unsolved. |
| [A2Tools](https://github.com/taengu/A2Tools-DPS-Meter) | Desktop meter and shared fight-report ecosystem. | Keep desktop capture/export versioning separate from server verification. |
| [cyberbadger6969/aion2-dps-meter](https://github.com/cyberbadger6969/aion2-dps-meter) at `414de5c1d5f3d45ce35701bd25229c2dba736bb6` | Structured 02 97 roster GS/CP and game-provided 01 91 boss-list timers; GPL-3.0. | Read protocol facts, compare against private evidence; do not use name-only GS/CP caches or speculative integer searches. |
| [SkeeveAN/Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter) at `86188a557943fef0861d011629c4bab30523fb92` | Separate character inspection, event schedules, reports and uploads; MIT. | Preserve distinct sources and timestamps. Inspection GS is not CP or automatically a combat identity. |

## Supplied private capture

The supplied 2026-10-14 validation log is inspected locally only. No raw packets,
player names, chat, endpoints or stable character IDs are included in this report.

- 333,806 log lines; 95 sampled party-roster frames and 35 field-boss-list candidate samples.
- Current strict roster reader accepted 93 of the 95 roster frames: 344 member
  records, 338 with nonzero GS and all 344 with nonzero CP. These are observations
  across time, not 344 distinct players.
- An exploratory decoder replay of all logged `raw` records produced 273
  score-linked identity events on five actor IDs. Raw records include repeated
  diagnostic payloads and multiple endpoints. This replay is useful for diagnosis
  but does not reconstruct the original transport stream or prove live party coverage.
- Current in-game character-sheet comparisons with matching timestamps remain
  unavailable. Parser acceptance proves internal consistency, not ground truth.

Keep structured server/stable-character linking, bounds checking, stale expiry,
and frozen historical scores. Missing identity evidence must stay unknown.

## Numbers and ranking integrity

Server-side schema validation does not prove truthful combat data. A desktop
signature embedded in a distributable client is not by itself a trusted verifier.
Verified rankings need a separately reviewed evidence format, capture completeness
rules, parser version/patch partitions, boss/phase/difficulty definitions, duplicate
fight identity, tamper analysis and moderation. This testing schema therefore
allows only `unverified`, even through direct SQL inserts.

Public presentation must say **captured all-target encounter DPS**. It must not
claim boss-only DPS, effective damage, full party visibility, a kill, a percentile,
or GS/CP-based comparability from unsupported evidence.
