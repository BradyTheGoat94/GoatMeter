# GoatSim timing foundation

Test branch only. Run `node --test tests/goatsim/*.test.mjs`.

`engine.mjs` is a deterministic, bounded, synthetic event model. Supply explicit skill cooldown and execution-lock durations in integer milliseconds, optional resource cost/gain, hotbar priority arrays (highest first), and macro slot steps. Only 10ms and 20ms macro polling are accepted. Basic attacks share the execution lock with macro/manual actions. Manual inputs are taps: blocked taps are recorded as skipped, not queued. At equal timestamps this model uses manual > basic > macro; this is an assumption, not measured client behavior. Cooldown starts at activation; resource changes occur immediately. End time is exclusive.

No actual class coefficients, skill damage, charge behavior, proc windows, network latency, animation cancellation, Skill Queue, or verified client timings are implemented. Cast counts are not damage or DPS. The existing build handbook's readiness model remains unchanged. Do not infer a winning class macro from synthetic fixtures.

Next: add provenance-bearing mechanic profiles, reject missing prerequisite mechanics, implement conditional windows and charged/manual scheduling, and compare Templar automatic/manual Doom Shield only after measured inputs are available. Use authorized logs for calibration while keeping raw private data out of commits. Add a runnable comparison report with explicit assumptions before exposing any UI or optimization recommendation.

## Required first benchmark: Patchwerk / stationary dummy

The first DPS comparison must use one stationary, continuously attackable target, full uptime, no adds, movement, encounter downtime or target switching. Hold duration, gear/stats, skill and stigma levels, selected specialties, buffs/debuffs, starting resources and initial cooldown state identical across candidates. Record the actual client patch and target defenses. Separate self-buffed and party-buffed scenarios. Use repeated trials for random procs once implemented and report variation. Start with a documented cold opener; any sustained/warm-start comparison must be a separate scenario. Never compare mismatched scenarios or interpret cast counts as DPS. Current timing-only engine has no target damage model and cannot yet produce this benchmark's game DPS.

## Character and target import contract

`profiles.mjs` validates Global character/loadout snapshots separately from a stationary dummy target. Unknown stats, skill levels and specialization choices use explicit `null`; zero is a real measured value, never a missing-data fallback. Snapshots require capture time and client patch information; a patch marked pending is not verification. `profileReadiness` checks the inputs requested by a mechanics model and always reports `gameDpsValidated: false`. Completeness is not correctness. No network fetcher or damage formulas are implemented.

Shugo.GG's FAQ (reviewed 2026-10-17, https://shugo.gg/faq) states there is no public API and skill specializations are not exposed by the upstream character API. Do not depend on undocumented internal routes. Support manual entry or an authorized import adapter, retain source provenance, and require the player to confirm missing specialties. Never derive target defense from player Combat Power or gear score.

## Sorcerer matched dummy comparison

`sorcerer-comparison.json` records only the four visible native macro inputs from the user-supplied berdugz screenshots. The stack contents and possible additional macro entries remain unresolved. It is not an executable preset; do not substitute four individual abilities for those stacked inputs. Keep both candidates unverified.

After confirming the full layout, compare against catalog `sorcerer/boss-manual`. Run five paired 120-second stationary single-target trials, alternating A/B then B/A to reduce order effects. Reset cooldowns/resources and use identical character stats, equipment, specialties, buff/consumable policy, target, patch, 10ms delay and Skill Queue setting. Test 20ms separately. Record manual charge/burst policy per candidate: changing it changes the whole configuration being compared, so any result is not attributable solely to macro ordering. If Flame Arrow is in the candidate macro, do not silently add a second held Flame Arrow input unless explicitly part of that candidate.

Use total damage to that same target divided by the fixed 120-second test window. Archive first-to-last observed target duration is a different denominator and must not substitute for this fixed window. Exclude adds; reject capture loss, death, movement or target downtime. Record cast attempts where available separately from damage hits; no cooldown-efficiency claim can be recovered from hit counts alone. Keep raw logs private.

`compareTrials` in `compare-trials.mjs` accepts only complete paired trial records with an identical explicit scenario fingerprint and confirmed layouts. It summarizes observed per-pair DPS differences and range; it does not establish statistical significance, independently validate data, or declare an optimal macro. No real trials have been supplied for this comparison, and no real result is available.

## Run the offline prototype

`node labs/goatsim/run.mjs labs/goatsim/synthetic-example.json`

Optionally append a new output filename to save the JSON report. Existing files are never overwritten. The example contains deliberately fictional skills and timings, not any AION class. Reports include a cast timeline, counts, skipped manual taps and remaining resources, with an explicit warning that they are not game DPS. This makes the current core runnable without needing a game account or Sorcerer character.

## Execution mode selector

Set `executionMode` to `macro` (default) or `apl` in the input JSON. Both use the same skill timings, resources, target-window duration, basic inputs and scheduled manual taps. Macro mode cycles `steps` and resolves each referenced hotbar stack. APL mode takes an ordered `apl` array, e.g. `[{"skill":"Synthetic spender","minResource":2},{"skill":"Synthetic generator"}]`, and picks the first usable matching rule. Supported rule conditions are `minResource` and `notBeforeMs`; unknown conditions are rejected. `decisionIntervalMs` controls APL reevaluation (default10ms, explicit model assumption, not a native macro delay).

Reports identify the selected mode. APL currently represents ideal priority execution, not a measured human: reaction delay, mistakes, latency and queue behavior remain unimplemented. Buff/proc conditions and charge handling still require implementation before class rotation claims. Native macro input remains restricted to10/20ms. A web UI selector is not yet published; this selection exists in the offline engine input. Comparing mode totals currently compares synthetic casts, not DPS or a guaranteed theoretical ceiling.

## Test interface

`ui/index.html` provides Macro/APL/both selection, shared duration/delay controls, an editable synthetic configuration, cast counts, first100-action timelines and JSON export. Serve the repository through an ordinary local HTTP static server and open `/labs/goatsim/ui/`; ES module imports/fetch are not designed for file://. No server has been launched as part of managed-environment QA. Browser visual verification remains pending. This interface is not deployed to production. The test CI artifact includes `labs/goatsim/` alongside the handbook so it can be evaluated without altering the live site.

## Stable event scheduling

The timing engine now uses `EventQueue` (`events.mjs`) for macro/APL decisions, basic input and scheduled manual taps. The queue orders by time, explicit priority and insertion sequence; the current input tie policy remains manual > basic > decision. Fight end is exclusive, and past scheduling and unbounded event loops are rejected. The event count now counts individual input events rather than distinct timestamps. The queue supports callback scheduling for future impacts/expirations, but those class combat effects are not implemented yet. No claim of observed Global event tie semantics is made.

## Synthetic impacts and buffs

Optional skill fields `impactUnits` and `impactDelayMs` schedule fictional impact values separately from activation. Optional `buff: {id, durationMs, multiplier}` activates immediately; a same-ID refresh replaces its earlier duration without an old expiry removing the refreshed buff. Multipliers from different IDs multiply. Impacts use buffs active at impact time, not a cast-time snapshot. A buff is inactive exactly at its expiry timestamp; expiry events precede impacts, which precede new input events. A newly cast buff affects its own immediate impact. These are explicit synthetic assumptions, not observed AION 2 mechanics. Both activation and impact must occur before the exclusive fight-end boundary to contribute.

Reports/export expose `totalSyntheticUnits`, per-skill units and impact events; UI labels them fictional and never calls them game damage or DPS. No stat coefficients, crits, defense, DoTs, pets, charge stages, buff caps or skill-specific snapshot behavior are inferred. Zero-delay effects are queued at the activation timestamp. Effects that would land after the test window do not contribute. Numeric bounds and event budgets reject runaway effects.

## Synthetic charges and conditional windows

A skill may specify `chargeMs`, `manualOnly`, or `requiresBuff` (a modeled window ID). Native macro/basic policies skip charged or manual-only skills; explicit manual taps or ideal APL may execute them. A charged action holds the shared lock for charge+recovery, and its impact occurs after charge+impact delay. Cooldown and resource cost start at activation in this model. Windows gate availability and expire exclusively at their end timestamp. The default is a full uninterrupted charge; variable stages, cancellation, refunds, consumed/reset windows and actual class proc behavior remain unsupported. Charged buff-granting actions are rejected until completion semantics are implemented. No AION 2 skill is declared mechanically verified by these synthetic tests.

## Private empirical capture replay (first data-connected mode)

Replay diagnostics report both aggregate covered-total error and hit-weighted absolute error (sum of absolute hit residuals divided by covered observed damage). Aggregate errors can cancel; neither metric validates a transferable game formula. Exclusions list unseen signatures versus insufficient training, without silently pooling different hit flags. Older bundles without diagnostics show hit error as unavailable, never zero. Regenerate locally to obtain diagnostics.

Generate a PRIVATE bundle with `python tools/build_empirical_replay.py /authorized/capture.log --class-name Templar --output /private/new-file.json`, then load it using the test UI's Local capture replay input. Python uses standard-library modules only. The output is local and exclusive-create: it will not overwrite an existing file. The generated bundle/report must never be committed or placed in public assets/artifacts.

This mode selects the largest confirmed single-actor/target/scope partition of the chosen class, uses earlier70% hits to estimate per-signature mean hit damage, then evaluates those means against later observed hits. Signatures preserve event kind, primary type and complete flags where available. A signature needs at least5 training hits; unseen/underpopulated types are excluded. Whole timestamp groups stay on one side of the split.

It reports covered-hit and covered-damage fractions, predicted versus observed damage on the covered subset, and absolute covered-total error. Window DPS divides that subset by the observed held-out first-to-last hit interval (plus1ms for an exclusive end), NOT a fixed120-second dummy window. This is neither full encounter DPS nor a future rotation prediction. Actual later hit times and type outcomes are supplied, so it does not test cast scheduling, crit probability, macro quality or build changes. It blocks permission flags for new rotations or gear scaling. A low covered error with low coverage is not an accurate total model; held-out errors can cancel. Buffs, stats, target defenses and capture revisions remain confounded.

Character snapshot/armory integration remains separate: preserve displayed stat units and provenance, require missing specialties/macros explicitly, and do not infer rating conversions from a screenshot. No undocumented Shugo requests are made.
