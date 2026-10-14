# GoatMeter

GoatMeter is a passive AION 2 DPS meter for Windows, verified against regression tests, reviewed captures and observed dungeon runs. Independent party-total comparison and broader protocol coverage remain outstanding.

## Build
GitHub Actions publishes a self-contained Windows x64 build. Run the **Build Windows EXE** workflow, then download the `GoatMeter-win-x64` artifact.

Extract the entire build ZIP and run `GoatMeter.exe`. Existing settings and history are preserved automatically.

## Live capture
Requires Npcap on the test PC. The application observes TCP/13328 passively and does not inject into or modify the game process.

See [accuracy and reliability status](docs/accuracy-reliability.md) for implemented features, timing rules, validation commands and remaining live-protocol work.


## Event and Spacetime Rift countdowns
Click **◷** in the overlay header for a separate countdown window. It includes Spacetime Rift entry-window countdowns and hourly Shugo Festival planning reminders, plus JSON schedule import. Each profile shows its timezone, source and review date. The selected profile and imported schedules are saved locally.

Global public reports disagree about rift opening anchors and server clocks. Three explicitly labeled community estimates are bundled (UTC 00:00 cycle, UTC+9 00:00 client-data report, UTC+9 02:00 cycle); the initial profile is the UTC estimate, not an inferred player region. Scheduled windows are calculations, not live opening confirmations. Entry windows and times can be corrected by importing a validated profile. Game notices take priority. This feature performs no network polling and sends no game packets.

## Gear score and combat power (testing)
Experimental passive GS/CP support is enabled in the testing protocol profile. It reads the structured party-roster layout and attaches values only through an explicit stable-character/session identity link, scoped by server when available. Unknown, ambiguous or expired values show **—**; no name-only lookup or DPS-based score estimate is used. This strict mode can leave scores unknown until the required identity packets arrive.

GS and CP are distinct values, shown below player rows in every preset and in detailed reports. Current/overall values expire after ten minutes without a fresh roster observation; completed encounters retain the values observed for that encounter. Identity reuse and server collisions cannot inherit old scores. Toggle display under Settings → Show GS and combat power. To disable the experimental decoder, set `experimentalRosterScores` to false in the protocol profile. Public fixtures validate structure and binding; an in-game GS/CP comparison on the current Global client remains pending.

## Shared overlay controls
Every preset has **Ⅱ** to freeze only the displayed snapshot (capture and fight history continue), **▶** to resume, and **▤** to copy the displayed fight summary to your clipboard. Selecting a different segment/category/history item or resetting resumes the display. Copying never sends a message; stream-safe name and GS/CP visibility preferences apply to the copied text. Interface scale is adjustable from 70% to 125% in settings and is saved with the other preferences. Existing open reports adopt privacy/score visibility changes.

## Overlay designs
All presets now expose fight/history selection, reset, countdown access and target information. **Neon Compact** adds slim contribution tracks and large rate values; **Pulse Cards** adds circular class badges and rounded player cards. **Midnight Gold**, **Rose Quartz**, and **Aurora** join the existing themes. Settings → Stream-safe player names replaces visible player names with class/rank labels; stored combat data remains unchanged.

Player rows also show **Target share** in Damage view: each player's portion of captured damage to the selected target, excluding damage to other targets. It is separate from the player's contribution to all captured damage and is not a claim that all world-boss damage reached this client.

Choose **Encounter Focus** for a large target name and encounter timer, aligned total/share/rate columns, thin class-colored ranking bars, a position/size lock, and a quick opacity slider. Columns follow the selected category (including Healing/HPS). The target HP bar appears only when maximum HP is known. Share means contribution to the selected category total, not boss-only damage. Position locking is also available in settings and is preserved between sessions.

This preset uses existing passive combat data. It does not claim world-boss spawn alerts, event schedules, PvP classification, gear scores, or local-player identity that the current data does not supply.

Choose **Modern Glass** in the overlay gear menu for rounded translucent panels, class-colored cards, larger metric values, and a compact encounter toolbar. New installations default to Modern Glass; existing saved style preferences are preserved.

Use **↻** in the header to reset the current fight immediately. The completed fight stays in history, overall totals remain intact, and incoming combat starts a new current fight. The action returns to Current Fight even when viewing Overall or a saved encounter.

Open the overlay gear menu and choose **Details Inspired** for full-width class-colored ranked bars, or **Kagerou Inspired** for translucent compact rows with thin contribution bars. These are independent row designs; color themes can be used with either style. Classic and the existing layout presets remain available.

The compact category selector supports all eight categories, and the encounter selector supports current, previous, and overall. Double-click a player in either design to open the detailed report. Drag the header to move the overlay. Style, theme, size, position, opacity, detail visibility, and always-on-top preferences are saved on closing settings, hiding the overlay, or exiting.

Validation logs now include `resolvedCombat` records showing the exact actor name delivered from capture to aggregation, alongside `captureIdentity` records. This helps distinguish missing live identity data from display or routing issues.
