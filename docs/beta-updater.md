# Installer updates and beta channel

The application checks on startup and every six hours while open. Users can also use CHECK FOR UPDATES. Checks do not interrupt capture; download and installation require confirmation. Concurrent checks in one app instance are suppressed.

Builds default to the Stable channel. Beta publishing must set the MSBuild UpdateChannel property/environment variable to Beta. This is stored in assembly metadata, independent of the combat decoder or party behavior. Stable checks GitHub releases/latest and rejects prereleases. Beta checks the most recent 100 releases, selects the highest compatible version and can move to a newer stable build. Only goatmeter-build-N stable tags and goatmeter-beta-N prerelease tags with matching official installer/checksum asset URLs qualify. Drafts, incomplete releases, and same/older versions are ignored.

The app downloads SHA256SUMS.txt and the installer, checks the hash, and removes incomplete files on failure/cancellation. Before starting installation it stops capture and waits for queued fight history saves. Inno Setup uses the existing per-user AppId/install location, closes affected applications, and preserves separately stored settings/history. Portable users accepting installer updates migrate to the standard installation.

## Bootstrap limitation

Public beta 218 was built with the old stable-only updater. Users of that beta need one manual installation of the first updater-enabled beta. They can then receive future compatible betas from within GoatMeter. It is not possible to add beta-channel behavior to the already installed 218 binary without replacing it. Stable production and its latest-release pointer remain unchanged.

## Verification

- 124 xUnit tests passed locally for Stable and Beta builds (including updater eligibility, channel metadata, official asset validation, verified download, missing/malformed/duplicate/mismatched checksums, truncated transfer, cancellation and separate attempts).
- 231 legacy combat regression assertions and 29 Python checks passed.
- A real public release check simulated an older beta, discovered beta 218, downloaded the 66,525,260-byte installer and verified its published checksum. The installer was not run locally.
- The beta build workflow runs protocol/updater checks and WPF smoke checks before publishing. Its disposable-runner installer test simulates the file-based Npcap prerequisite check (without installing/loading a driver), installs public beta 218, upgrades in the same directory to the new build and checks a user-data marker. A failure prevents publishing the beta.

Future releases must include GoatMeter-Setup.exe and SHA256SUMS.txt with the expected version/tag convention. Publish beta builds through the beta workflow so assembly channel metadata is correct. Stable production builds remain Stable by default.
