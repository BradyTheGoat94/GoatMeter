# Sparse party roster decoder validation

Branch: `test/goatsim-integration`, starting at `f6f655a`.

The October 14 local combat log contains 95 sampled `partyRoster` frames.
Two 11-byte messages contain no complete roster and remain rejected. The
other 93 frames decoded to 344 member observations before this change and
407 after it. These are repeated observations, not 407 distinct players.
The original log was replayed locally; private bytes and names are not
included in the repository.

Numbered vacant slots occupy 35 bytes and can occur between occupied slots.
Stopping at the first vacancy lost later members. Searching for zero bytes
could also overwrite an already found occupied-slot boundary. The decoder
now walks numbered vacancies, prefers structural numbered boundaries, and
validates compact zero sentinels through all advertised remaining slots.
The vacant prefix is checked so a role byte inside a vacant record cannot
masquerade as another slot header. Ambiguous boundaries remain rejected.

Synthetic tests cover sparse occupancy, numbered vacancies with role bytes,
truncated vacancies, contradictory compact suffixes, and GS/CP preservation.
No capture bytes or player names are used in those fixtures. Scores retain
the existing experimental flag and exact character/server identity joins;
this replay does not establish current in-game score accuracy or party-leave
semantics. The existing ten-minute membership expiry is unchanged.

The two test projects previously shared `tests/obj/project.assets.json`.
Restoring the legacy net8.0 runner could overwrite the xUnit Windows target,
causing NETSDK1005 on the next no-restore test run. Project-specific build
folders now isolate their dependency metadata. CI also runs combat checks
on this test branch, including the Python GoatSim suite.

Validation commands, from the repository root:

```
dotnet test tests/Aion2DPSPro.Tests.csproj -c Release
dotnet run --project tests/Regression.csproj -c Release
dotnet test tests/Aion2DPSPro.Tests.csproj -c Release --no-restore
python -m unittest discover -s tests -p 'test_*.py' -v
dotnet run --project tests/OverlaySmoke/OverlaySmoke.csproj -c Release
```

Overlay smoke windows are placed offscreen without activation while retaining
rendering and layout checks. They disable preference persistence to avoid
depending on or changing the user's saved overlay settings. Main and public
releases are not changed.
