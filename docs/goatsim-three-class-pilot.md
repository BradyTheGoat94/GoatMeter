# GoatSim three-class volunteer pilot (test branch only)

**Status:** tester protocol; not an announcement of an integrated in-app exporter or calibrated model.

## Cohort
- **Templar:** Bradyboi; use as initial calibration and regression baseline.
- **Spiritmaster:** volunteer; prioritize direct damage vs DoT vs summoned actor attribution.
- **Gladiator:** volunteer; prioritize direct attacks, AoE target fan-out, and skill cooldowns.

## Capture protocol (each class)
1. Record region, game patch, class, level, equipment/build snapshot and timestamp before the session. Record changes between fights.
2. Use a **test/lab build only**, kept in a separate directory. Do not replace public GoatMeter.
3. Collect at least three distinct, reasonably clean encounters against a named repeatable boss, if practical, and one training-dummy or solo-target session if available. Do not assume logs contain all damage.
4. Preserve original logs **locally**. Review before sharing. Do not upload raw captures by default; they may contain other players' identities or network metadata.
5. Record boss name, approximate start/end, deaths, party size, notable buffs, and whether the player was actively attacking. These annotations aid encounter boundaries.
6. If an opt-in evidence exporter is available, preview the exported fields, redact unrelated identities, and share only with explicit consent. Current Python CLI is not yet integrated into the game UI.
7. Hold back at least one encounter per player/session from model fitting, for independent validation.

## Special validation checks
### Templar
- Verify Judgment proc windows, Corrode specialization duration, Punishment damage attribution, and skill identity consistency.
### Spiritmaster
- Confirm pet/summon owner mapping, DoT ticks vs application hits, re-summon actor IDs, and damage during pet-only activity.
### Gladiator
- Verify multi-target skill damage is attributed once per hit and that AoE isn't misread as repeated casts.

## Acceptance gates
- **Parser**: source identity is session-scoped; duplicate/boundary events are handled; unknown attribution is explicitly flagged.
- **Privacy**: no upload without consent; reviewable export preview; no unrelated player names, chat, IPs or raw packet payloads.
- **Calibration**: separate train and holdout encounters; report per-skill and encounter-level prediction errors; never invent an optimal DPS percentage.
- **Release**: do not merge into production or publish a public download until tests and real-player review pass.

## Requested pilot feedback
For each test session, report: class/level, game region and patch, encounter target, whether meter remained stable, whether skills and pets were attributed correctly, and any incorrect names or missing damage.

## Immediate engineering tasks
- Identify real encounter history storage and UI classes in the test repository before integrating.
- Add local preview dialog and explicit export action.
- Validate the Python export schema against actual sanitized encounter records.
- Add tests for Spiritmaster pet attribution and Gladiator AoE fan-out.
- Run Windows lab build and CI; keep experimental artifact clearly marked.

No volunteer needs to install an unfinished CLI or share personal information to start preparing.
