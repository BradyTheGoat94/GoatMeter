# Evidence calibration workflow

Use `python tools/calibrate_skill_hits.py /authorized/local/capture.log` for a private aggregate report. Do not commit or publish generated reports or raw captures.

The miner pairs confirmed-class damage/resolved events on timestamp/source/target, keeps direct/DoT/summon and reported type separate, and internally partitions actor/target/skill context. It suppresses exact duplicates using the existing diagnostic-log convention. Each sufficiently populated cohort uses its first70% of chronological hits for a median baseline and the remainder as holdout, without splitting identical timestamps. Outputs include hit counts and held-out errors, not raw lines, names or actor/target identifiers.

This is a stationarity diagnostic, not a damage formula fit. A low median error does not validate game mechanics: crits, buff changes, gear swaps, changing target defenses, encounter transitions, multi-hit grouping and decoder revisions can all confound it. Cohorts with fewer than30 hits or insufficient holdout are omitted; this does not establish absence of that class or skill. No macro attribution, cast frequency, cooldown utilization or actual game DPS is inferred. Chronological same-context holdout is weaker than separate encounter/profile validation.

Inventory local captures before interpreting them. Small files with repeated/template-like record counts need provenance review; do not automatically treat every uploaded file as an independent live session. Preserve decoder/capture version where available. Missing profiles remain unknown. Next analysis should interpret reported type only after verifying its decoder semantics, identify encounter/buff context, and test supported formulas across separate contexts rather than fitting one global median.
