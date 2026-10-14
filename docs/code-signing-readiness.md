# GoatMeter code-signing preparation

**SignPath application paused:** the owner wants free application use with separately paid commercial code reuse. This is a restrictive licensing model, incompatible with SignPath Foundation's open-source requirement. See [proposed licensing policy](proposed-licensing-policy.md). Do not add an open-source license or submit this application without a changed owner decision.

Status: application not submitted, approval not granted, release 168 unsigned. No SignPath certificate or project/API credentials are configured. This preparation does not alter the public build or grant a software license.

## Confirmed readiness

- Public repository: https://github.com/BradyTheGoat94/GoatMeter
- Public website: https://thegoatmeter.us
- Automated Windows builds and installer/portable releases exist; latest reviewed main checkpoint is 2e4a2692a4829aeedc5b1a65852ecc6bb3c881be.
- Windows regression checks and overlay render checks passed for that checkpoint.
- Release 168 installer matches its published SHA-256 checksum. Its PE certificate table is empty; this is not a malware-scan result.
- Npcap is installed separately, not bundled by the installer.

## Decisions and verification before applying

1. Owner selects an OSI-approved license for the original code. There is currently no root license file. Do not add a license until the owner approves its rights and obligations. Third-party code and assets retain their original terms.
2. Audit dependency notices and provenance of bundled class icons, logo and game-name/data assets. A code license alone does not establish rights to third-party material. Resolve any incompatible or unlicensed material before representing the whole project as eligible.
3. Confirm repository owner and all signing participants use multi-factor authentication. Do not infer account security from public repository access.
4. Define actual authors/reviewers and release-signing approvers. Owner approves each signing request, as required by SignPath Foundation.
5. Publish a accurate privacy and signing policy after project approval. Until then, label signing as pending; do not claim SignPath provides signatures for current downloads.
6. Apply at https://signpath.org/apply with the repository, website, release links, functionality description and team details. SignPath alone decides eligibility. Any required account authentication is completed by the owner.

## Integration after approval

- Use SignPath-assigned organization/project/artifact identifiers; never invent them.
- Keep API credentials in GitHub Actions secrets, never source or logs.
- Set matching product versions on the application and installer for each release.
- Build the app from reviewed source, submit its executable for signing and await authorized approval.
- Build the installer from the signed application, submit the installer for signing and await authorized approval.
- Verify Authenticode signatures and timestamping on returned files. Fail publication if required signatures are missing or invalid.
- Package the signed app, then compute SHA-256 checksums of the final signed installer and portable archive. Publish only those exact artifacts; do not modify executable bytes afterward.
- Keep existing public release usable until the signed replacement passes checks. Signed publishing must not silently fall back to unsigned output.

## Accurate application description

GoatMeter is a Windows AION 2 combat analysis overlay. It observes local game network traffic through a separately installed Npcap driver; it does not inject into the game, read game memory, send gameplay commands or automate gameplay. It provides combat breakdowns, saved fights, themes and local schedule estimates. Passive score metadata remains experimental and is joined only to exact observed character/server identity. Privacy claims must be checked against the complete release and its dependencies before submission.

## Sources

- Eligibility and required signing policy: https://signpath.org/terms
- Application: https://signpath.org/apply
- Microsoft SmartScreen reputation: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation

Trusted signing identifies a publisher/build origin; it is not a malware-free guarantee and does not promise instant SmartScreen reputation.
