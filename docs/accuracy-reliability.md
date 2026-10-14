# Accuracy and reliability

The meter remains experimental. A passing build and replay regressions do not certify protocol accuracy or game-policy compatibility. Profile verification remains false.

Implemented: background bounded capture queue; separate directional TCP framers; candidate flow validation; reconnect resets; modular/overlap-aware TCP reassembly; bounded gaps and stale-flow cleanup; frozen completed encounters; atomic history saves; Current/Previous/Overall datasets; actor-specific skills; independent healing/damage-taken/count/effect aggregation; duration-based buff uptime; explicit ownership; entity-removal cache cleanup and reuse protection; hex-color theme fix.

Timing: encounter duration spans first to last accepted combat event, with a one-second minimum for a single hit. Overall DPS uses summed encounter durations and excludes inter-fight idle time. Active DPS estimates activity with inter-event intervals capped at five seconds. Normal inactivity defaults to 30s and is configurable in overlay settings. Explicit trusted boss events use 120s and boss-death completion; the live decoder currently has no verified boss classification, so real live fights use the normal timeout. Manual completion is available in settings.

Run `dotnet run --project tests/Regression.csproj -c Release` for combat, identity, history, framing and TCP regressions. Four raw frames from the latest validation log are replayed against logged damage and entity IDs. These detect changed decoder behavior; their expected values come from the existing decoder, not independently verified full-fight ground truth. No character names or complete private logs are committed.

On Windows run `dotnet run --project tests/OverlaySmoke/OverlaySmoke.csproj -c Release` for WPF construction, distinct theme colors, style restoration and segment/category switching. GitHub Actions runs these checks before publishing.

Remaining live validation:

- Complete controlled fights with independent expected totals, including party members, pets, DoTs, healing, transitions, wipes and reconnects. The existing 500-line diagnostic sample is not a full capture or ground-truth report.
- Verified live opcodes for buffs/debuffs, deaths, interrupts, dispels, combat state, zones and boss classification. Aggregators exist, but those tabs will remain empty until verified events arrive.
- Party/global/session identity mapping, class inference, summon ownership and damage flag semantics checked against controlled captures. Names alone are not unique identities.
- Windows/Npcap capture, reconnect and overlay behavior tested in actual gameplay.

Health distinguishes waiting, idle/disconnected, packets without decoded events, and queue overload. Packets without events can mean idle game traffic or decoder mismatch. Missing TCP bytes are discarded, never bridged into a fake frame. Gap or overload warnings mean the encounter can be incomplete.

Limits: healing is raw decoded healing, not verified effective healing/overheal. Automatic boss lifecycle remains unverified, so long mechanics can exceed the inactivity timeout. Effect sources are separate. Per-encounter recent events are capped at 2,000 and in-memory completed history at 100. Saved aggregates remain available on disk. Observed removal/reconnect conservatively splits entities, so Overall can contain multiple rows for the same character until a verified persistent identity key is available.

## Identity routing and report update

Exact PlayerName/entity mappings from identity-only TCP/13328 conversations are now bridged into the selected combat flow within the same capture adapter, local IP and server IP. Pre-lock identities are replayed when combat is validated. Entries expire after ten minutes and observed primary-flow despawn removes them. No name guessing or cross-server merge is performed. Character lookup using another server IP still requires independent correlation evidence. Identity diagnostics alone are not proof that a live party session is fully resolved.

The dedicated report refreshes every 500ms, can pause, exposes category-specific datasets, and remains tied to the encounter it opened. Skill totals include hit/crit statistics, contribution, min/mean/max and observed hit-flag counts. Target totals are aggregated over the complete accepted encounter rather than inferred from the bounded event feed. Buff/debuff empty states explain unverified live opcodes. The event feed is still bounded to 2,000 encounter events.


## GS/CP provenance and identity validation plan (2026-10-14)

Public AionFlex methodology distinguishes combat power measured with a fight, a nearby profile snapshot, and today's profile estimate. These are not equivalent observations and must never be silently mixed. Source: https://aionflex.gg/meta/methodology . This is a research reference, not copied implementation.

GoatMeter currently attaches experimental scores only through explicit server/character/session links. Keep this rule: never assign a roster value by display name alone, never infer GS from CP or damage, and never treat public profile values as observed live roster values. A malformed declared roster member (including an empty name) invalidates the entire roster record; tests enforce this.

Future UI/serialization changes should distinguish `observed-in-session`, `profile-snapshot`, and `unknown` as separate provenance states if profile lookup is ever added. A fight report must freeze its own observed score and timestamp. Live score expiry should not rewrite a completed report; historical profile estimates must be labeled and opt-in, not substituted for fight observations. Distinguish score freshness from correctness: an older observation is not proof the character's score changed.

Validation checklist before claiming Global support: same-name characters across servers, reused local actor IDs, score update arriving before and after identity, reconnect and cache expiration, zero/absent CP, incomplete multi-member roster, server collisions, and comparison against in-game GS and CP. No third-party code or data was imported.
