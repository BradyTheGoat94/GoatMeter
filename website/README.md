# GoatMeter build handbook — test-only candidate

This directory stages the next handbook for the existing Site. It is not deployed and does not contain or replace the homepage, archive, database, hosting manifest or public downloads.

Source branch: `labs/goatsim-rankings-updater-20261016`.
Entry: `builds/index.html`, served with `website/` as the static root. Use a local static server only when permitted by the execution environment. No package installation or build step is required.

## Included

- Eight Global class pages, 53 selectable content/configuration worksheets.
- One-key native damage-line candidates; separate manually timed boss burst.
- PvP, solo, dungeon/raid and leveling/fresh-45 modes. Cleric additionally separates healing and Chanter-present composition. Unsupported exact configurations are visible as research and cannot be copied.
- Exact row 0–3 previews, selected-line copy plans, 10 ms default / 20 ms alternative, character-level and equipped-skill-tier gating, manual charge exceptions.
- Four independent evidence labels. No candidate is mechanically verified, log-tested as a macro, or benchmark-validated.
- An abstract priority model with unavailable/cooldown inputs. This only checks the displayed priority contract; it does not emulate the game, animations, Skill Queue, proc eligibility, resources or buff application.

Templar's requested candidate is Q bottom-first Judgment / Shield Smite / Warding Strike / Doom Shield, then E Annihilate / Debilitating Smash / Pummel. Its comparison preset keeps Doom Shield manual. Both keep Punishment manual. Confirm Judgment's selected specialty, stack legality and Doom Shield's client behavior before assigning mechanical verification.

Sorcerer's eight-step published candidate is separated from the controlled burst setup. It is not presented as a safe copyable recommendation. Hellfire stays manual in every mode. Spiritmaster does not require four different spirits by assumption; charged Fusion moves off automatic lines. Cleric's overlapping-mantra replacement remains unresolved and cannot be copied as a complete build.

## Checks

```
node --check website/builds/app.js
node --test tests/builds/*.test.mjs
python -m unittest discover -s tests/builds -p 'test_*.py'
```

The branch-only CI performs these checks and uploads the static candidate artifact. It never deploys a Site or launches a desktop overlay. Existing Windows workflows are unchanged and are not invoked by this work.

## Remaining validation

The environment has no authorized running Global client and no supported browser-preview capability. In-game stack execution, queue ON/OFF, exact skill budgets, buff stacking and fair repeated benchmarks remain pending. Missing Global PvP sources are identified in the relevant modes. A level-45 character is never assumed to have skill level 16.

For authorized logs, `python tools/analyze_build_hits.py /path/to/private.log` emits aggregate hit diagnostics. Never commit the input or generated private report. Cast opportunities, gear and macro configuration are absent, so hit frequency is not cooldown efficiency. No synthetic DPS gain, optimality percentage or verified ranking is generated.

After actual gameplay validation and explicit publication authorization, port only reviewed handbook files into the existing Site workflow, preserving its root homepage, downloads and D1 routes. Do not deploy this standalone directory as a replacement Site.
