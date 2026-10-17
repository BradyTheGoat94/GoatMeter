# Raidbots / SimulationCraft architecture review

Reviewed 2026-10-17. This is a design assessment, not a claim to know Raidbots' private production backend or to have implemented SimC parity.

## Verified public architecture

Raidbots states it runs SimulationCraft underneath. SimC describes an event-driven combat simulator. Character profiles supply gear/talents/stats; the SimC addon can export more complete/current data than the WoW armory. An action priority list is conditional: the first available action executes, not a permanently fixed rotation. Class modules, spell data, resources, cooldowns, buffs, debuffs and target state determine availability and outcomes. Repeated stochastic fights estimate averages and sampling error. Identical seeded inputs reproduce results; a seed does not make differing profiles experience equivalent favorable rolls. Profile sets compare controlled variants of a baseline profile.

Sources (primary):
- https://support.raidbots.com/article/69-why-isnt-my-spec-supported
- https://support.raidbots.com/article/54-installing-and-using-the-simulationcraft-addon
- https://github.com/simulationcraft/simc
- https://github.com/simulationcraft/simc/wiki/ActionLists
- https://github.com/simulationcraft/simc/wiki/DevelopersDocumentation
- https://github.com/simulationcraft/simc/wiki/GameClientData
- https://github.com/simulationcraft/simc/wiki/StatisticalBehaviour
- https://github.com/simulationcraft/simc/wiki/ProfileSet

## GoatSim equivalent and gaps

| Component | Current foundation | Needed for credible AION 2 DPS |
|---|---|---|
| Character profile | Global snapshot validation and explicit unknowns | Authorized import plus actual stats, gear effects, skills, specialties; never infer from CP |
| Target | Stationary/full-uptime profile contract | Measured level/defense/resistance and target modifiers |
| Scheduler | Deterministic input timeline, cooldown/resource gates, shared lock | Separate cast start/completion, impacts, ticks, buff expiration; measured animation and queue rules |
| Decisions | Hotbar priorities and manual taps | Separate conditional ideal APL from native macro input policy; actual stack and polling semantics |
| Class mechanics | None validated | Versioned per-class formulas, resets, procs, buffs, DoTs, summons and charge stages |
| Damage | No formula | Validated coefficient/stat/defense/crit/multi-hit pipeline with rounding rules |
| Randomness | None | Seeded independent stochastic trials and uncertainty; synthetic fixtures first |
| Comparisons | Paired fixed-window observed-trial summary | Controlled profile-set simulation and consistent fight/buff policy |
| Reports | Synthetic cast timeline and counts | Per-skill damage, uptime, resource starvation, failed inputs, distributions and assumptions |
| Validation | Unit tests only for simulation | Held-out controlled logs; per-skill timing/damage and aggregate error across multiple stat profiles |

## Implementation order

1. Preserve the current runnable timing prototype and its tests. Add a stable discrete-event queue with explicit tie semantics before impacts/ticks/procs.
2. Introduce versioned mechanic profiles with evidence references and unknown-field blockers. Never silently supply guessed Global constants.
3. Add an independently authored synthetic damage pipeline and deterministic RNG to test event/statistics plumbing. Fictional examples stay visibly fictional.
4. Implement one supported Templar stationary-dummy model: Judgment windows, Doom Shield automatic/manual comparisons, Annihilate stack, Pummel and manual Punishment charges. Missing rules block real DPS output.
5. Add Monte Carlo batches, mean/variance/confidence reporting and bounded compute. Sampling error describes randomness in the model, not accuracy versus the game.
6. Calibrate on controlled captures and validate on separate captures. A good fit to one log is not validation of all gear or classes.
7. Add gear/stigma/specialty comparisons and more classes only after their modules meet the same gates. Website job queue/UI comes after trusted core behavior.

The user's initial benchmark remains fixed-duration (120-second comparison trials), one stationary dummy, full uptime, no adds or movement, identical stats/buffs/resources/cooldown start state. Warm-start sustained and cold-opener scenarios must remain distinct. Whole build changes compare configurations, not isolated macro causality.

## Reuse boundary

Use the publicly described architecture, with original GoatSim code. No SimulationCraft code or WoW data has been copied into this prototype. SimC's repository lists GPL-3.0 and additional dependency licenses: incorporating source requires a separate license review and compliance, not an assumption that public means unrestricted. Raidbots UI/branding/private infrastructure is not being copied. This review does not grant rights to AION 2 data or establish publisher approval.
