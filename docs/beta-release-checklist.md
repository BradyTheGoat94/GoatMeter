# GoatMeter Beta Release Checklist

Beta builds must remain separate from stable releases. Do not merge the audit branch into main merely to distribute a beta.

## Before publication

1. Verify Combat Regression Tests, Windows EXE Build and overlay smoke tests are green for the exact commit.
2. Validate an extended AION 2 session, including capture shutdown, packet loss warnings, encounter resets, party filter and GS/CP unknown-value handling.
3. Build an installer with a beta version suffix. Confirm it does not overwrite a stable installation without an explicit user decision.
4. Publish a GitHub Release marked **prerelease**, using a beta-specific tag. Never mark a beta as the latest stable release.
5. Include the installer, portable ZIP and SHA256SUMS.txt. Verify both downloaded hashes.
6. Only after release and installation checks, add a Beta download link to thegoatmeter.us. Label it experimental and retain the stable download.

## Auto-update acceptance criteria

- Stable installations check only stable release metadata by default.
- Beta installations check the beta channel; no implicit channel switching.
- Never silently install an update. Verify download integrity and ask the user before closing or replacing files.
- Do not update from GitHub Actions artifacts: these may expire or require authentication. Use release assets.
- Preserve settings, overlay configuration and encounter history.
- Test no-update, newer-version, unavailable-network, malformed metadata, hash mismatch, interrupted download and rollback/retry.
- Do not advertise automatic updating until the installed application has passed these scenarios.

## Website

Keep stable and beta buttons visually distinct. The beta link must point to a verified published prerelease installer, not a branch ZIP or an unverified placeholder.
