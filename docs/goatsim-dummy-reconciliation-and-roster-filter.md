# GoatSim: 2026-10-17 Templar dummy reconciliation and roster filters

## User-confirmed observations
- Bradyboi Templar, ~2-minute focused training dummy.
- Normal in-game priority macro with minimal manual activation.
- GoatMeter UI showed **17.1k DPS**.
- Another class was visible in the meter because it currently discovers nearby combatants, not just party/force.
- Requested view scopes: **Self**, **Party**, **Force**, **Everyone Nearby**.

## Provisional log analysis (not yet independently reproduced)
Earlier chat-level parse reported 3,216,768 attributed damage / ~126.09s = 25,512 DPS, 549 events. This differs by ~49% from the displayed 17.1k DPS. **Do not calibrate simulator to the 25.5k number until reconciled.** A 2-minute window would also not reconcile it (~26.8k).

### Investigation checklist
1. Establish exact selected encounter start/end, actor ID, target ID, and timestamp basis; compare same window with UI.
2. Confirm whether UI shows current encounter vs overall, selected player vs everyone, and whether idle time or pre-pull time is included.
3. Detect duplicate network captures from multiple Npcap interfaces and packet replay/identity aliases; dedupe by strong event fingerprint rather than timestamp alone.
4. Verify owner attribution, especially DoT (Punishment), skill 3001016, proc and secondary effects. Separate direct, DoT, reflected, environmental, and unknown.
5. Distinguish damage to actual training dummy from incidental nearby enemies or other players; do not use target HP candidate records as proof of damage.
6. Record count/amount excluded at each stage and make a traceable reconciliation table.
7. Keep raw capture local. Logs can include third-party player names, IP addresses and packet payloads; never commit them to public GitHub.

## Filter design (test branch)
- **Self Only**: resolve local player identity from capture scope, fail closed if unknown; do not guess by class.
- **Party Only**: membership must come from reliable game roster data or explicit local mapping; if unavailable label unavailable, never silently approximate using proximity.
- **Force Only**: likewise require verified force roster.
- **Everyone Nearby**: existing discovery scope.
- Filtering affects *display* and *GoatSim selected source*, not packet capture or encounter evidence retention.
- Persist chosen view scope locally, default **Self Only** in GoatSim; support user-visible unknown-membership status.
- Target filter and actor filter are separate: for dummy calibration require selected actor **and** selected dummy target.
- Never alter production/public releases.

## Acceptance criteria
- Reconcile 17.1k against independently decoded damage within same encounter; report unexplained delta honestly.
- Unit tests: nearby non-party player excluded from party view; force includes party; self-only one actor; unknown roster does not claim a valid party/force filter.
- Validate against second clean dummy run before model fitting.
