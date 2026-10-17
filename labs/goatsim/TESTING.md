# GoatSim test prototype — local only

This is a source/static test artifact, not a new Windows meter installer. No production site or download was changed. Real forward AION 2 DPS is **not ready**. Two separate modes are usable:

| Mode | What runs | What it cannot establish |
|---|---|---|
| Synthetic Macro / ideal APL / Compare both | Shared event engine with fictional skills and impact units | Real game DPS, best macro or measured human performance |
| Private capture replay | Earlier within-context hits estimate later damage given the actual later hit times/types | New rotations, new gear, target-defense scaling or independent dummy validation |

## Quick start

Extract the test artifact. Run commands from its root, containing `labs` and `tools`. Use Node 24 (the CI-tested version) and Python 3.12 for capture processing. No npm or pip packages are needed.

For a synthetic command-line smoke test:

```sh
node labs/goatsim/run.mjs labs/goatsim/synthetic-example.json
```

For the interactive interface, use your existing local static HTTP server with this root and open `/labs/goatsim/ui/`. If Python is installed, one local-only option is:

```sh
python -m http.server 8000 --bind 127.0.0.1
```

Open `http://127.0.0.1:8000/labs/goatsim/ui/`. Stop the server with Ctrl+C. It serves files beneath the current folder: **do not put private captures or reports there**. It has no upload endpoint. No browser visual QA or server launch was performed in the managed development environment; the CLI paths and automated tests were exercised instead.

Keep the duration at 120 seconds, select Macro / ideal APL / Compare both, and retain the default 10ms delay (20ms is the only alternative). Results are fictional impact units/casts, not game damage. Ideal APL is a policy benchmark, not a human-play simulation.

## Analyze an existing authorized capture locally

Replace the quoted paths below with your actual paths. Keep inputs and outputs outside the extracted artifact/repository. Existing outputs are never overwritten.

```sh
python tools/build_empirical_replay.py "PATH/TO/combat.log" --class-name Templar --output "PRIVATE/new-bundle.json"
node labs/goatsim/replay.mjs "PRIVATE/new-bundle.json" "PRIVATE/new-report.json"
python tools/audit_empirical_contexts.py "PATH/TO/combat.log" --class-name Templar --output "PRIVATE/new-context-audit.json"
```

The first command streams a raw diagnostic log and selects its largest confirmed class actor/target/scope partition. The second produces a headless replay report; omit its output argument to print privately to your terminal. Alternatively choose the bundle using **Local capture replay** in the test UI. Neither interface uploads the file. Replay JSON inputs are limited to 1 MiB; the streaming Python raw-log builder is separate from that limit. Other log formats, including arbitrary game log files, are not supported.

Read damage coverage, absolute hit error, aggregate error and excluded skills together. Unknown flag records stay unknown. Direct hits and DoTs remain separate; plentiful DoT samples do not validate a charged direct hit. Replay duration follows observed held-out hits, not the 120-second synthetic benchmark.

## Coverage and blockers

All eight classes have locally evaluated within-context empirical evidence in the accessible larger captures; see `class-coverage.md` for qualifications. Only Templar received the deeper private JS-estimator/skill-level check. No class has validated forward Macro/APL game DPS. Capture/client versions, matched character stats/specialties, target defenses, buffs, charge stages, scheduling and transferable damage formulas remain unresolved. No automatic Shugo import exists. Character snapshot validation preserves unknowns; it does not supply missing mechanics.

Next acceptance gate: establish a context-matched Templar stationary single-target model with the direct Punishment and Doom Shield evidence gaps addressed, then compare forward policies against held-out observations. Do not lower evidence thresholds, pool unrelated contexts, or call a low covered-total error overall accuracy.
