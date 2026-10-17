# Evidence calibration workflow

Use `python tools/calibrate_skill_hits.py /authorized/local/capture.log` for a private aggregate report. Do not commit or publish generated reports or raw captures.

The miner pairs confirmed-class damage/resolved events on timestamp/source/target, keeps direct/DoT/summon and reported type separate, and internally partitions actor/target/skill context. It suppresses exact duplicates using the existing diagnostic-log convention. Each sufficiently populated cohort uses its first70% of chronological hits for a median baseline and the remainder as holdout, without splitting identical timestamps. Outputs include hit counts and held-out errors, not raw lines, names or actor/target identifiers.

This is a stationarity diagnostic, not a damage formula fit. A low median error does not validate game mechanics: crits, buff changes, gear swaps, changing target defenses, encounter transitions, multi-hit grouping and decoder revisions can all confound it. Cohorts with fewer than30 hits or insufficient holdout are omitted; this does not establish absence of that class or skill. No macro attribution, cast frequency, cooldown utilization or actual game DPS is inferred. Chronological same-context holdout is weaker than separate encounter/profile validation.

Inventory local captures before interpreting them. Small files with repeated/template-like record counts need provenance review; do not automatically treat every uploaded file as an independent live session. Preserve decoder/capture version where available. Missing profiles remain unknown. Next analysis should interpret reported type only after verifying its decoder semantics, identify encounter/buff context, and test supported formulas across separate contexts rather than fitting one global median.

## Decoder type audit

Checked-in `src/Protocol/PacketDispatcher.cs` has a lossy `DecodeType` precedence (Crit, Perfect, Double, Parry, Back, Frontal, Direct), while `DecodeFlags` independently combines critical/modifier/direction flags. `damageFlags` is logged before the matching damage event. Thus a primary Crit label alone cannot separate critical+back from critical+front or critical+perfect samples.

`calibration_events.py` joins flags to damage using timestamp/actor/target/skill and checks logged booleans against the logged raw type/modifier/direction fields. Inconsistent or absent fields remain unknown, not normal. Internal groups now also include capture scope to avoid reusing an actor ID across scopes. Summon diagnostic markers lacking source/target/amount context are not treated as standalone damage events; owner-rewritten joins without a matching flag record remain unknown. This audit verifies consistency with our decoder, not the correctness of game protocol interpretations.

The private rerun changed which groups meet minimum sample size. New/old cohort counts are not an improvement metric: splitting modifiers can lower sample coverage, and selected surviving groups differ. Keep reports private and compare like-for-like events before assessing predictive error.

## First private replay checkpoint

The newer authorized capture was successfully processed by the private bundle builder and JS estimator. It produced nonempty Templar training models and held-out coverage/error diagnostics. Generated data and numerical reports remain private, outside the repository; only synthetic fixtures test this path in CI. No class receives formula-validated or benchmark-validated status. Improve uncovered signatures and isolate encounter/stat/buff context before any forward Macro/APL damage claims.

`audit_empirical_contexts.py` extends this to separate within-context holdouts rather than pooling actor/target/scope partitions. Largest hit gaps and spans expose possible discontinuities without declaring arbitrary gap thresholds to be encounter boundaries. Unsupported contexts are counted explicitly. This is not leave-one-context-out transfer validation; do not equate separate rows with independently matched dummy trials. The common builder rejects mixed contexts and thresholds below five training hits.
