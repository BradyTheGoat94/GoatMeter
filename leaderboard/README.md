# GoatMeter fight archive — testing MVP

This is an isolated feature for a future `https://thegoatmeter.us/logs/` route.
Nothing in this directory deploys itself. Production has not been changed.

## Run locally

Node.js 24 or newer is required. The local service has no third-party runtime dependencies.

```powershell
cd leaderboard
npm start
```

Open `http://127.0.0.1:8787/logs/`. The server binds only to loopback. SQLite
records persist in ignored `data/fights.sqlite`. This is a local development
server, not an Internet-facing production server.

```powershell
npm test
npm run build
```

`build` writes only static assets under ignored `dist/logs`. It does not deploy.

## Desktop-to-web flow

1. Finish an encounter and open a player's combat report for that saved fight.
2. Select **Export fight**. Current combat, simulated previews, and Overall are rejected.
3. Save the JSON. It contains character names, known server IDs, class, per-target captured damage,
   start time and observed durations. Raw events, skills, network packets, stable character IDs,
   GS and CP are excluded by an explicit allowlist.
4. Choose that JSON in the archive, select the recorded boss target, annotate the fight name, and select privacy.
5. Save the returned access receipt. It allows access, visibility changes and deletion.

The 124 MB validation `.log` supplied for protocol analysis is **not** an upload
format. It remains private. `example-fight.json` is explicitly labeled sample data.
Do not represent sample values as measured gameplay.

The community ranking metric is captured **selected-target damage / observed target duration**.
Boss selection is uploader supplied, not independently verified. This is not a kill confirmation, effective damage, or an independently
validated party total. The encounter label is user supplied. Scores are comparable
only within the limits of their capture completeness and manually supplied labels.

## API

| Route | Behavior |
| --- | --- |
| `GET /api/health` | Schema, upload availability and verification availability. |
| `POST /api/fights` | Strict JSON upload, 64 KiB maximum, 1–24 players, 1–64 targets, 1–7200 seconds. |
| `GET /api/catalog` | Boss labels from public fights only. |
| `GET /api/leaderboard?mode=community&boss=…&class=…&limit=50&offset=0` | Public unverified rows, server-computed DPS, bounded pagination. |
| `GET /api/leaderboard?mode=verified` | Always empty in this MVP. |
| `GET /api/fights/:id` | Public access, unlisted share key or private owner key. |
| `PATCH /api/fights/:id` | Owner-only visibility update with `{"visibility":"private"}`. |
| `DELETE /api/fights/:id` | Owner-only deletion. |

`upload.schema.json` documents the format. `validation.mjs` additionally rejects
control characters, duplicate names on the same or unknown server, future start times, nonfinite numbers,
unsupported fields, and zero-damage encounters. No uploader-supplied DPS, trust
flag, raw identity field or verification status is accepted.

The browser generates a cryptographically random 32-byte hexadecimal owner key
before upload and passes it in `Authorization: Bearer KEY`. Repeating the same
payload with that key recovers the same receipt without duplicating rankings.
Changing the payload requires a new key. Clients that omit the header receive a
server-generated key, but cannot recover a lost initial response without it.

## Privacy and trust model

- Exports include character names and known servers. Private is the desktop and web default. Public records alone enter the community table.
- Unlisted share keys are read-only. Owner keys can read private records, change visibility and delete.
- Only hashes of keys persist in the database. Receipts deliberately contain the
  access keys; keep them private. Losing the receipt loses management access in
  this accountless MVP. Restoring a receipt does not send it anywhere except this API.
- Keys in links use the fragment, not query strings, and travel to the API through
  a bearer header. The client uses no analytics, remote fonts or localStorage.
- HTML/CSS/JS use a same-origin CSP, no-referrer policy and text-only rendering
  of uploaded strings. Cross-origin mutations with an Origin header are rejected.
- `unverified` is enforced in the database constraint, API, detail page and UI.
  There is no verification endpoint, admin shortcut, automatic promotion or
  plausible-score heuristic. A future verifier requires a separate reviewed migration.
- Local uploads are limited to 10/minute per local client. The Cloudflare adapter
  requires a configured rate-limit binding and fails closed without one. Its IP
  limit is coarse abuse protection, not authentication or a global quota.
- The archive has a hard insert cap of 10,000 fights, enforced atomically. That
  is a testing safeguard, not a finished retention policy or service-level promise.

## Website integration after explicit approval

The current public domain responds through Cloudflare. The origin/deployment
account and the exact live website source are not confirmed. `docs/index.html`
in the desktop repository differs from the inspected production page. Both the
old `Aion-2-Pro-Dps-Meter` URL and `GoatMeter` URL resolved to the same repository HEAD.
Do not replace production's homepage with this feature.

`worker.mjs` adapts the **same tested API** to Cloudflare Workers + D1. The prepared
`wrangler.example.jsonc` intentionally has no production routes, no database ID,
no workers.dev URL, no public preview URL, and uploads disabled. It cannot be
used unchanged to create a public service. No deployment command is in package
scripts, and no deployment credentials are stored here.

After approval and authenticated hosting access:

1. Confirm the existing host and its routing. Use a separate staging database
   and non-production host first; never bind the testing service to the live DB.
2. Apply `schema.sql` to that staging database and configure DB, ASSETS, and
   a unique UPLOAD_LIMITER namespace. Decide abuse controls, retention, backups,
   restore testing, moderation and owner-account recovery before public opening.
3. Build `dist`; run staging browser/API checks and enable uploads explicitly.
4. Review the homepage navigation change separately, linking **Fight logs** to
   `/logs/`. Reserve only `/logs/*`, `/api/fights*`, `/api/leaderboard*`,
   `/api/catalog` and `/api/health` for this service, after checking existing routes.
5. Confirm production approval, then apply reviewed routing/configuration. Verify
   anonymous/private/unlisted access from a clean browser and test rollback.

Workers/D1 behavior was checked against Cloudflare's primary documentation:
[assets bindings](https://developers.cloudflare.com/workers/static-assets/binding/),
[prepared statements](https://developers.cloudflare.com/d1/worker-api/prepared-statements/),
[rate limits](https://developers.cloudflare.com/workers/runtime-apis/bindings/rate-limit/).

## Known MVP limits

No identity-verified characters, percentiles, difficulty/patch partitions,
verified boss classification, signed capture provenance, guild authentication, or automatic
desktop background uploader. Upload receipts support request retry deduplication;
they cannot prove that differently exported files represent the same real fight.
The trusted leaderboard stays closed until that broader verification design is ready.

## Current v2 boss-ranking behavior

Desktop exports now retain character names, known server IDs, per-target damage,
and per-target observed duration. They deliberately leave rankingTargetId null.
Choose a recorded boss in the upload page before submitting. The API requires a
real target reference and rejects target totals exceeding each player's encounter
damage. Target IDs in exports are opaque labels, not raw game entity IDs.

Public community rankings use only the selected target's damage divided by its
first-to-last captured player-damage window (one-second minimum). Timings are
aggregated from accepted events and do not depend on the 2,000-event recent feed.
Other-target damage is excluded. A selected target is still not a verified boss
identity or kill, and uploads remain unverified. Legacy v1 logs remain readable
but cannot enter boss rankings. Old desktop history without target timing needs
new captures before it can supply a v2 ranking.

The existing production site was located in prior Sites deployment history:
project appgprj_6ac310a815bc8191b9bcff40c7700de8. Publishing must use that existing
project's current source and bindings, preserving its homepage and domain.
Sites management/publishing tools are unavailable in this chat at this checkpoint.
The standalone Cloudflare adapter is a tested portability option, not the confirmed
production deployment route. Do not create a replacement site or point the domain
to a new Worker merely to bypass missing Sites access.
