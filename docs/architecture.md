# NanoVault architecture

## Solution layout

| Project | Target | Purpose |
| --- | --- | --- |
| `NanoVault.Core` | net8.0 | Domain models, service interfaces, pure policies (path sanitisation, organisation templates, metadata merge, iPod path normalisation), pause token. No I/O. |
| `NanoVault.Ipod` | net8.0 | iPod detection scoring, discovery service, read-only iTunesDB/iTunesCDB parser, scanner. Depends only on `IReadOnlyFileSystem`. |
| `NanoVault.Infrastructure` | net8.0 | Real filesystem, TagLibSharp reader, SHA-256 verification, duplicate resolver, planner, copy engine, executor, playlists, reports, settings, volume providers/monitors. |
| `NanoVault.App.ViewModels` | net8.0 | All view-models (CommunityToolkit.Mvvm). Cross-platform so state-transition tests run anywhere. |
| `NanoVault.App` | net8.0-windows | WPF views, themes, DI composition root, Serilog bootstrap. No logic beyond presentation glue. |
| `NanoVault.Installer` | — | Inno Setup (`.iss`) and NSIS (`.nsi`) installer definitions. |

## Read-only-by-construction

The filesystem abstraction is split:

```csharp
IReadOnlyFileSystem   // exists/enumerate/stat/OpenRead only
IFileSystem : IReadOnlyFileSystem   // adds writes — destination side only
```

Everything that touches the iPod (discovery, database reader, scanner,
metadata reader, and the *source* side of the copy engine) is compiled against
`IReadOnlyFileSystem`, so writing to the device is a compile error, not a
code-review hope. TagLibSharp is additionally wrapped in a file abstraction
whose `WriteStream` throws.

Source files are opened with `FileMode.Open`, `FileAccess.Read`,
`FileShare.ReadWrite | FileShare.Delete`.

## The backup pipeline

```text
IVolumeInfoProvider ─▶ IpodDiscoveryService ─▶ IpodDevice(s)
                                   │
                                   ▼
                     IpodScanner (files + TagLib tags
                        + ITunesDbReader fallback)   ─▶ ScanResult
                                   │
                                   ▼
                     BackupPlanner (+ OutputPathBuilder,
                        DuplicateResolver, free space)  ─▶ BackupPlan
                                   │
                                   ▼
        BackupService ─▶ BackupExecutor ─▶ TrackCopyService (per track)
             │                                   copy → verify → rename
             ├─▶ M3u8PlaylistWriter (master + iPod playlists)
             └─▶ BackupReportWriter (HTML + JSON)          ─▶ BackupResult
```

Copies stream through a `.nanovault-partial` file, hash while copying,
re-hash the written file when verification is on, then rename atomically.
A vanished device flips the run into `Interrupted`; completed files are kept
and a re-run resumes by detecting size+hash matches at the destination.

## iTunesDB parsing

The classic database is a tree of little-endian chunks
(`mhbd → mhsd → mhlt/mhlp → mhit/mhyp → mhod/mhip`). The reader validates
every length and offset before use, drops malformed records individually,
sanity-checks numeric fields (year, track/disc numbers, 16.16 fixed-point
sample rates), decodes UTF-16 strings with byte-length validation, rejects
path traversal in locations, and transparently inflates `iTunesCDB` zlib
payloads — all strictly read-only. `tests/NanoVault.TestSupport/ITunesDbBuilder`
writes real databases for round-trip and corruption tests.

## UI state machine

`MainViewModel` owns the flow: `Searching → Found / MultipleFound /
StorageUnavailable / NotFound / DisconnectedDuringBackup`, screen navigation
(`Home / Tracks / Progress / Completion / Settings / Troubleshooting`),
pause/cancel tokens, and re-planning on destination, selection, or settings
changes. Views bind to it; services are injected; nothing in a view-model
touches the filesystem or Windows APIs directly (`IFolderPicker`,
`IShellService`, `IUiDispatcher` abstract the rest).
