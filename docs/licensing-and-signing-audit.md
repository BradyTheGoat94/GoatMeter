# Licensing and signing audit — 2026-10-14

Preparation only. Current public release is not re-licensed or newly signed by this audit. There is no submission or Store approval.

## Confirmed observations

| Item | Observation | Remaining check |
| --- | --- | --- |
| Original repository | Public; no root LICENSE at reviewed main checkpoint 2e4a2692 | Confirm licensor and contributor rights; adopt reviewed terms only after asset audit |
| SharpPcap | Application references version 6.2.5; current upstream dotpcap/sharppcap has LICENSES/MIT.txt | Inspect the exact shipped NuGet package license/notices and dependencies, not merely current upstream |
| PacketDotNet | Used by capture code; current dotpcap/packetnet reports MPL-2.0 | Confirm actual resolved version and preserve its required notices/source obligations |
| Npcap | Installer checks for separately installed driver and opens official download page; does not bundle driver | Retain separate installation; do not imply redistribution permission or include it in MSIX |
| External NPC catalog | PublicGameData downloads npc_names_en.json from Helveticxa/Aether-Aion2-DPS-meter-Global, a GPL-3.0 repository | Inspect file-specific provenance/permissions. Repository metadata alone does not settle rights in underlying game data. Obtain appropriate permission or independently permitted replacement if needed |
| Other game-name data | PublicGameData fetches api.aion2meter.com/api/gamedata/meter-bootstrap; exact-ID names also reference Aion2Flow public resource pack | Establish source terms and attribution; public access does not itself grant a license |
| Class/brand images | Bundled class PNGs and goat logo; no asset license/provenance files found in inspected tree | Confirm original creation or actual redistribution permissions |
| Nightmare theme | Theme draws a recognizable requested character motif using WPF geometry | Original drawing code does not settle underlying character-art rights; assess or replace for proprietary distribution |

Do not conclude the whole application must be GPL merely because an external catalog comes from a GPL-labeled repository. Do not conclude the catalog can be freely relicensed either. Its provenance and use need review.

## Privacy corrections needed for final published policy

Source inspection confirms outbound game-name requests to api.aion2meter.com and raw.githubusercontent.com. Avoid blanket claims that GoatMeter performs no remote requests. These requests retrieve catalogs; they are distinct from sending gameplay inputs or uploading combat logs. Confirm full current-release behavior, dependency behavior and request metadata before finalizing privacy wording.

Observed cache location: LocalApplicationData/Aion2DPSPro/meter-bootstrap.json. Cached names can be read when the bootstrap request fails. Name data is metadata; licensing changes must not alter combat totals or silently degrade English labels.

## Free signing with proprietary code

Microsoft documents free Store signing for certified MSIX submissions. Its MSI/EXE submission route still requires publisher-provided trusted Authenticode signing. Store signing does not create a general certificate for the website's standalone EXE.

GoatMeter has a significant MSIX gate: it requires the Npcap Windows driver. Microsoft's desktop packaging preparation guide says MSIX does not support Windows drivers, and also flags elevation and interactive prerequisite installation. GoatMeter currently prompts for a separate manual prerequisite installation. Do not represent a Store package as working until Npcap access, installation policy and Store certification eligibility are resolved.

Testing a manifest alone does not prove packaged live capture works. Acceptance needs a Windows clean-user install, Npcap dependency handling, capture as a standard user, settings/log access, transparent topmost overlay behavior and uninstall/update behavior. Owner must complete Store identity/account verification and Microsoft must accept the package. No guaranteed free trusted signature for the existing standalone installer has been established under the requested restrictive license.

## Sources checked

- https://github.com/dotpcap/sharppcap/tree/master/LICENSES
- https://github.com/dotpcap/packetnet/blob/master/LICENSE
- https://github.com/Helveticxa/Aether-Aion2-DPS-meter-Global
- https://npcap.com/oem/redist
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options
- https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare
- https://learn.microsoft.com/en-us/windows/apps/publish/faq/open-developer-account

## Current deliverable

GoatMeter-EULA-DRAFT.txt contains proposed free application-use terms with separately negotiated paid commercial reuse. It is not operative. PR44 remains draft, SignPath preparation remains paused, source/binaries remain public as before, and public download remains build168.
