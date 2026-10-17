# GoatUpdater architecture audit

GoatMeter uses .NET 8 WPF, a Windows x64 self-contained single-file binary, and a per-user Inno Setup installer. GitHub Actions publishes releases only from main. The current SHA256SUMS file is not authenticated by a pinned signing key. No in-app update flow was found. User fight history lives in LocalAppData/Aion2DPSPro/History, outside the install directory.

Production must stay unchanged while testing the updater on this branch.
