# GoatSim: cross-class evidence and calibration protocol (experimental)

## Goal
Build an evidence-backed simulator for all eight AION 2 Global classes without requiring the project owner to play each class. Bradyboi Templar is the reference implementation, **not** a universal damage model.

## Input contracts
1. **Character profile**: class, region/build/version, character level, timestamp, attack and penetration (retain raw values and display rounding), crit rate, damage bonuses, combat speed, cooldown reduction, skill ranks/specializations, equipment/passives, source URL and capture time. Unknown values remain null, never zero.
2. **Combat event**: capture/session ID, timestamp, source actor ID, target actor ID, skill ID/name, amount, damage type, crit marker if actually observed, ownership attribution confidence, parser version, raw evidence pointer. Distinguish direct hits, DoTs, summons, healing, and ambiguous damage.
3. **Encounter**: explicit start/end rationale, boss/target IDs, interruptions, participant map, capture completeness indicators, identity-map provenance, and event membership.
4. **Calibration record**: model version, training encounter IDs, holdout encounter IDs, skill-level observed vs predicted error, uncertainty interval, known missing mechanics, and provenance.

## Opt-in community export
- Offer 'Export to GoatSim' as a **local preview-first** action; no automatic uploading.
- Export a versioned JSON/ZIP schema, with only selected character stats, build, and selected encounter events.
- Exclude packet payloads, chat, account IDs, IP addresses, tokens, machine identifiers, unrelated players' identities and names by default.
- Replace other actors with per-encounter pseudonyms; use random local export IDs.
- Show event counts, time range, file size and exactly what will be shared before upload; require explicit consent.
- Allow deleting submissions and withdrawing future use where operationally possible; document retention and dataset licensing.

## Data sources
- First-party consenting GoatMeter submissions are preferred.
- Public repositories and tools (A2Tools, RATmeter, other meters) are references for parser behavior and tests; do **not** paste GPL source into a proprietary codebase without a license decision.
- Shugo.gg character and skill pages are candidate reference data; verify permission/API/robots/terms before automated collection. Store URL, timestamp and patch/region.
- Community logs must be licensed or submitted with clear permission. Avoid copying personal or player-identifying data.

## Calibration algorithm
1. Verify ordered segment reconstruction and identity map scoped by capture/session. Actor IDs can be reused; do not merge sessions based only on integer ID.
2. Deduplicate source segment overlaps, preserve line fragments, and count malformed records.
3. Separate combat windows conservatively, then classify boss encounters with target and continuity evidence. Never call an arbitrary activity window a boss fight.
4. Derive per-skill damage distribution, hit frequency, DoT attribution, and buffs. Mark missing critical flags and unknown defense instead of inferring them.
5. Split by encounter and preferably by player/session into training, validation, and untouched holdout sets. Never split individual events from one encounter across train/test.
6. Calibrate skill coefficients only where inputs are known. Track confounding from target defenses, gear changes, buffs, latency, and parser loss.
7. Report per-skill MAE/MAPE where denominators are valid, encounter damage error, and confidence intervals. Publish no '90% optimal' claims without measured evidence.
8. Use Templar as a pipeline regression test, then onboard classes by evidence quality, not a fixed accuracy promise.

## Evidence tiers
- **Observed**: exact log events and documented character snapshots.
- **Calibrated**: validated on independent held-out encounters with reported error.
- **Provisional**: plausible mechanics or external reference with incomplete independent testing.
- **Unknown**: no adequate evidence; simulator must disclose missing inputs.

## Next implementable milestones
- [ ] Add versioned `goatsim-export-v1` JSON schema and strict validator.
- [ ] Add local privacy preview and pseudonymization.
- [ ] Add session-aware identity mapping and segment-boundary tests.
- [ ] Add encounter-level train/holdout split with leakage tests.
- [ ] Produce a Templar per-skill observed-vs-predicted report.
- [ ] Add one consenting non-Templar reference dataset; verify class-specific mechanics before expanding.
- [ ] Gate accuracy badges on reproducible holdout evaluation.

This document is a design specification, not proof that these features are implemented or validated.
