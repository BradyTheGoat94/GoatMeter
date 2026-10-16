# Boss-targeted leaderboards checkpoint

Branch: feature/fight-log-leaderboards-mvp.
Character names and known server IDs are now included in desktop exports and
ranking rows. Private remains the default. Public records remain unverified.

Schema v2 exports each target separately and retain a full-aggregate observed
target window, independent of the 2,000-event recent feed. The upload page requires
selection of a recorded target. Leaderboards use that target's damage and duration;
adds and unrelated boss damage are excluded. Detail pages can switch targets.
Legacy v1 all-target logs remain readable and are excluded from boss rankings.

Verified: 11 API/HTTP tests pass; 248 desktop assertions pass; source checks and
asset build pass. The desktop compiled successfully after target timing was added.
No desktop UI tests were launched. Browser rendering for the new target selector
remains unverified. This is not a verified boss classifier or a confirmed kill.

Existing production site located in previous deployment history:
appgprj_6ac310a815bc8191b9bcff40c7700de8.
The Sites publishing tools are absent in this chat; no reconnect management tool
is available. A browser fallback at chatgpt.com/sites was signed out. No account,
connection, production source, database or deployment was changed. Deployment is
blocked by missing authenticated Sites access, not by missing hosting details.

Public deployment was requested conditionally on working boss-specific data.
The next step is to open the existing Site with authenticated Sites tooling,
inspect its current source and bindings, integrate /logs and the target API,
verify on a private saved/staging version, and publish the reviewed existing Site.
Do not create a replacement site or change the domain to bypass access.
