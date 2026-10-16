# Combined score, export, history and capture-integrity test build

Keeps public main and release downloads unchanged. Based on the GS/CP test branch; ports only the tested fight-export/per-target timing additions from supplied local snapshot e9222ea.

- One Windows test binary includes late/exact GS/CP linkage and Export fight.
- Score detail tooltip explains exact server, observation timestamp/age, experimental roster source, expiry and frozen historical values. Stream-safe mode does not expose character names/IDs through this tooltip.
- History loads have a 16 MiB read budget, structural checks and per-file corruption warnings. Writes are atomic and serialized. Settings allow 50–5000 active fights (500 default); excess moves to History/Archive, without deleting captures. 512 MiB/10,000 JSON files total stops new saves with a warning. Limits and Archive instructions appear in overlay settings.
- Packet queue drops, reassembly resets and capture reconnects mark active/pending encounters incomplete. Metadata cannot start fights or alter damage. Stored and exported snapshots retain counts; no-known-loss means only that these detectors saw no loss, never a verification claim. Older histories remain unknown. Attribution is conservative: drops seen before the first accepted hit are carried into the next fight.
- Export remains private by default, with names/classes/known servers but no stable character IDs, raw packets or chat. Boss target selection remains explicit on the website. All uploads remain unverified.
- The matching existing Site accepts optional captureIntegrity fields, rejects inconsistent counts and shows incomplete/unknown/no-reported-loss notices without upgrading verification.

Validation: 284 desktop regression assertions (including target timing/export privacy, retention/archival/roundtrip, oversized/corrupt/traversal rejection, provenance and capture loss). Windows compilation/publication runs in branch-only workflow without launching OverlaySmoke or desktop windows. Website has 12 API/HTTP tests plus built-Worker integration checks. Live game values and visual interactions still require user testing.
