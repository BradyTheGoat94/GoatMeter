# Current class coverage

Reviewed 2026-10-17. No class has validated stat-scalable AION 2 DPS formulas. Common engine tests do not grant class support.

| Class | Current evidence-connected status | Forward macro/APL game DPS |
|---|---|---|
| Templar | Private capture-conditioned replay generated and evaluated; incomplete held-out damage coverage | Blocked pending formulas, character/target context and scheduling validation |
| Gladiator | Empirical-only: within-context holdout bundles evaluated locally; no class rotation model validated | Blocked |
| Assassin | Empirical-only: older accessible capture has within-context holdout evidence; absent from the newer capture, not absent overall | Blocked |
| Ranger | Empirical-only: within-context holdout bundles evaluated locally; no class rotation model validated | Blocked |
| Sorcerer | Empirical-only: within-context holdout bundles evaluated locally; source macro still has unresolved complete stacks | Blocked |
| Spiritmaster | Empirical-only: within-context holdouts available, including older capture; this is a different threshold/test from the earlier calibration-cohort audit | Blocked |
| Cleric | Empirical-only: within-context holdout bundles evaluated locally; healing/support effects not modeled | Blocked |
| Chanter | Empirical-only: within-context holdout bundles evaluated locally; party buffs not validated | Blocked |

The generic replay builder accepts a class filter but must be run successfully on sufficient actual evidence before a class is described as replay-tested. The expanded inventory evaluated local Python holdouts across the two larger accessible captures without pooling them. This establishes data availability, not client-tested class scheduling, matched stats or transferable formulas; capture/client versions remain unknown. Smaller captures with unresolved provenance are not treated as independent benchmark trials. Character-sheet values and selected specialties still require a supported import or manual confirmation. No names, raw captures, private profiles or empirical sample data are bundled in the test artifact.

Next: inspect uncovered Templar signatures and capture/encounter boundaries, add robust skill/name/flags mapping, compare alternate estimators on identical held-out events, and connect only supported mechanics to a forward model. Do not extrapolate replay means to new gear, target defenses or a different hit/rotation schedule.
