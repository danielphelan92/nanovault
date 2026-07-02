# Claude Code Build Specification: iPod Nano Music Backup for Windows

## Project name

**NanoVault**

A polished Windows desktop application that detects an Apple iPod nano (especially the 4th generation) and copies the music stored on it into a normal, organised folder chosen by the user.

The app must be simple enough for a non-technical user:

1. Connect the iPod.
2. Open NanoVault.
3. Choose a destination folder.
4. Click **Back Up Music**.
5. Receive an organised copy of the music.

Do not require the user to reveal hidden folders, browse `iPod_Control`, rename files manually, use command-line tools, install Python, or understand how an iPod stores music.

---

## Core instruction to Claude Code

Build the complete, production-quality application described in this document. Do not stop after creating a mock-up, prototype, design, or partial implementation. Implement the working detection, scanning, metadata extraction, copying, naming, duplicate handling, verification, logging, cancellation, installer, and automated tests.

Work autonomously through the specification. Make sensible engineering decisions where a small detail is unspecified. Do not replace difficult functionality with fake data or placeholder buttons.

After each major phase:

- Build the solution.
- Run the tests.
- Fix compiler warnings and errors.
- Test the primary workflow.
- Commit the work with a clear Git commit message.

Do not modify, initialise, restore, sync, format, erase, or write to the connected iPod. The iPod must always be treated as a **read-only source**.

---

# 1. Product objective

Create a native Windows application that safely exports music from an iPod nano to a folder on the PC.

The application must:

- Detect a connected iPod nano automatically.
- Support the Apple iPod nano 4th generation as the primary target.
- Support other classic disk-based or flash-based iPods where they expose the standard `iPod_Control` structure.
- Find the hidden music files without requiring Windows Explorer changes.
- Recover useful track names and metadata.
- Copy tracks into a clean folder structure.
- Preserve the original audio without transcoding or reducing quality.
- Show clear progress and results.
- Be installable on Windows through a proper installer.
- Look modern, trustworthy, and polished.
- Work without requiring iTunes during normal operation whenever Windows already mounts the iPod as a readable volume.

The software is for backing up music the user owns or is authorised to copy. It must not attempt to bypass DRM or decrypt protected media.

---

# 2. Supported platform

## Required

- Windows 10 22H2, 64-bit
- Windows 11, 64-bit
- x64 installer
- Standard user account where possible
- .NET 8 LTS or the newest stable .NET LTS available when implementation begins

## Preferred technology

Use:

- **C#**
- **WPF** for the desktop interface
- **MVVM** architecture
- Dependency injection through `Microsoft.Extensions.DependencyInjection`
- `CommunityToolkit.Mvvm`
- `TagLibSharp` for supported audio metadata
- `Serilog` for structured application logs
- `xUnit` for tests

Use a self-contained Windows x64 release so the end user does not have to install .NET separately.

Prefer a standard signed-ready `.exe` installer generated with **Inno Setup** or **WiX Toolset**. An MSIX package may additionally be supplied, but the normal installer must not create avoidable certificate installation problems for users.

---

# 3. Scope and safety boundaries

## The application may

- Enumerate Windows storage volumes.
- Read files and directories from a detected iPod.
- Read the iPod database and media metadata.
- Copy media to a folder selected by the user.
- Create folders and files inside the selected destination.
- Generate logs and backup reports on the PC.

## The application must never

- Delete anything from the iPod.
- Rename files on the iPod.
- Change file attributes on the iPod.
- Update `iTunesDB` or any iPod database.
- Synchronise music back to the iPod.
- Restore or format the iPod.
- Automatically launch an Apple restore process.
- Attempt DRM removal, FairPlay decryption, password bypassing, or ownership circumvention.
- silently overwrite existing PC files.

Open source files using read-only access and permissive sharing wherever possible:

```csharp
FileMode.Open
FileAccess.Read
FileShare.ReadWrite | FileShare.Delete
```

No service or background process should continuously access the iPod after the application closes.

---

# 4. How classic iPod music storage must be handled

Classic iPods and older iPod nanos commonly expose music under a hidden folder similar to:

```text
<iPod drive>\iPod_Control\Music\F00\ABCD.mp3
<iPod drive>\iPod_Control\Music\F01\EFGH.m4a
```

The physical filenames can be random and must not be used as the final exported names when metadata is available.

The application must search case-insensitively for:

```text
iPod_Control\Music
```

It should also detect likely iPods using several signals rather than relying only on a volume label:

- Presence of `iPod_Control`.
- Presence of `iPod_Control\Music`.
- Presence of `iPod_Control\iTunes\iTunesDB` or related database files.
- Volume label containing `IPOD`.
- Relevant USB/removable-volume information exposed by Windows.

Do not assume the iPod always has the drive letter `E:` or that the volume label has not been renamed.

## Disk mode guidance

When an iPod is connected but no readable `iPod_Control` volume is available, show a friendly troubleshooting screen. Explain that the device may need to appear in Windows as a drive or have disk use enabled. Do not tell the user to restore or erase it.

The troubleshooting screen should suggest:

1. Unlock or wake the iPod if applicable.
2. Try a known-good Apple-compatible USB cable.
3. Try another USB port without a hub.
4. Close iTunes or Apple Devices if it is locking the device.
5. Enable disk use in the appropriate Apple device-management software where supported.
6. Reconnect the device and click **Scan Again**.

---

# 5. Music discovery and metadata strategy

Use a layered metadata strategy.

## Layer 1: Embedded audio tags

Read embedded tags from every supported file using TagLibSharp or an equivalent reliable library.

Extract where available:

- Title
- Artist
- Album artist
- Album
- Disc number
- Track number
- Year
- Genre
- Composer
- Duration
- Bitrate
- Sample rate
- File format
- Artwork presence

## Layer 2: iPod database fallback

Implement a read-only iPod database reader for the database files under:

```text
iPod_Control\iTunes
```

Use it to associate the iPod's internal media path with track metadata where embedded tags are missing or incomplete.

The reader must be defensive:

- Validate lengths and offsets before reading.
- Never trust malformed values.
- Fail an individual record rather than the whole scan.
- Log parsing warnings without exposing confusing technical details in the normal UI.
- Keep all database access read-only.

Create an abstraction such as:

```csharp
public interface IIpodDatabaseReader
{
    Task<IReadOnlyDictionary<string, IpodTrackRecord>> ReadTracksAsync(
        string ipodRoot,
        CancellationToken cancellationToken);
}
```

Normalise path separators and handle internal iPod paths that use colons instead of Windows backslashes.

## Layer 3: Safe fallback naming

When neither embedded tags nor database metadata provide a title, retain the original file but give it a stable fallback name such as:

```text
Unknown Artist\Unknown Album\Unknown Track - ABCD.mp3
```

Never discard a readable track solely because its metadata is incomplete.

---

# 6. Supported media

Copy recognised music and spoken-audio files without conversion.

Initially support discovery and copying of:

- `.mp3`
- `.m4a`
- `.m4b`
- `.aac`
- `.wav`
- `.aiff`
- `.aif`
- `.aa`
- `.alac` where encountered

Also recognise `.m4p` as potentially protected AAC.

For protected or unreadable files:

- Do not attempt decryption.
- Mark the item as **Protected or unsupported**.
- Let the user include it in a raw backup if it can be read.
- Explain that playback may still require the original authorised Apple account or compatible software.

The first release is focused on audio. Do not mix photos, contacts, games, videos, or device firmware into the normal music backup.

---

# 7. Export organisation

Provide three organisation choices in **Settings**, with the first as default.

## Default

```text
Artist\Album\01 - Track Title.ext
```

## Alternative 1

```text
Album Artist\Year - Album\01 - Track Title.ext
```

## Alternative 2

```text
All Music\Artist - Track Title.ext
```

For multi-disc albums use:

```text
Disc 1\01 - Track Title.ext
```

or prefix the filename clearly when disc subfolders are disabled.

## File and folder sanitisation

Replace Windows-invalid characters safely:

```text
< > : " / \ | ? *
```

Also handle:

- Trailing periods and spaces.
- Reserved Windows names such as `CON`, `PRN`, `AUX`, `NUL`, `COM1`, and `LPT1`.
- Extremely long paths.
- Empty metadata fields.
- Duplicate track names.

Prefer readable names. Do not unnecessarily replace ordinary punctuation.

Enable long-path-aware application settings where appropriate, but keep generated relative paths to a practical length.

---

# 8. Duplicate handling

Before copying, determine whether a destination file already exists.

Offer these options:

- **Skip exact duplicates** — default.
- **Keep both** — append ` (2)`, ` (3)`, and so on.
- **Replace destination file** — only after an explicit user selection.

Use a fast staged strategy:

1. Compare expected destination path.
2. Compare file size.
3. Compare a cryptographic hash only when needed.

Use SHA-256 for verification and exact duplicate confirmation. Hash streams asynchronously and support cancellation.

Never silently overwrite a file.

---

# 9. Backup workflow

## Start screen

Show:

- App logo and name.
- Headline: **Back up music from your iPod**.
- A device status card.
- Destination folder selector.
- Main **Back Up Music** button.

### Device card states

1. **Searching for an iPod**
2. **iPod found**
3. **More than one iPod found**
4. **iPod connected but storage is unavailable**
5. **No iPod found**
6. **Device disconnected during backup**

When found, show useful non-sensitive details where available:

- Device or volume name
- Drive letter
- Capacity
- Free space
- Estimated music count
- Estimated music size

Do not show serial numbers by default.

## Pre-backup scan

After an iPod is found:

- Scan music files in the background.
- Read metadata.
- Build the proposed output paths.
- Detect protected, unreadable, missing, and duplicate items.
- Display a summary before copying.

Summary example:

```text
1,842 tracks found
17.6 GB to copy
12 exact duplicates will be skipped
3 protected files will be copied as-is
2 files could not be read
```

## Backup progress

Display:

- Overall percentage.
- Current track.
- Tracks completed versus total.
- Data copied versus total.
- Current transfer speed.
- Estimated remaining time, labelled as an estimate.
- Pause button.
- Cancel button.

Write each track to a temporary partial filename in the destination, for example:

```text
Track Name.mp3.nanovault-partial
```

After a successful copy and optional verification, atomically rename it to the final filename. Remove abandoned partial files when safe.

## Completion screen

Show:

- Successful track count.
- Skipped duplicate count.
- Warning count.
- Failed count.
- Total copied size.
- Elapsed time.

Buttons:

- **Open Backup Folder**
- **View Report**
- **Back Up Another iPod**

Use reassuring wording when the backup completes with minor warnings. Do not label the entire operation a failure because one damaged track could not be read.

---

# 10. Track selection

The simplest workflow is **Back up everything**, but include an optional review screen.

Allow filtering and selecting by:

- All tracks
- Artist
- Album
- Genre
- Playlist, where reliable playlist data can be read
- Search text

Columns:

- Checkbox
- Title
- Artist
- Album
- Track number
- Duration
- Format
- Status

Use virtualisation so thousands of tracks remain smooth.

The default selection must be all discovered music.

---

# 11. User interface and visual design

Create a modern Windows desktop design, not a generic grey developer utility.

## Visual direction

- Calm, clean, premium appearance.
- Rounded cards and controls.
- Good spacing and hierarchy.
- Light and dark modes following the Windows preference.
- Subtle animation only where it improves understanding.
- Fluent-style icons.
- Clear success, warning, and error states.
- No unnecessary menus or technical jargon.

## Suggested palette

Use a restrained neutral interface with a tasteful purple/blue accent inspired by classic iPod colours. Ensure accessible contrast.

## Accessibility

- Full keyboard navigation.
- Visible focus indicators.
- Screen-reader labels.
- Do not communicate status through colour alone.
- Support Windows display scaling from 100% to 200%.
- Minimum practical window size around 900 × 620.
- Resizable layout.

## Key wording

Prefer:

- “Back up music” instead of “extract database objects”.
- “Choose backup folder” instead of “set output path”.
- “This track could not be read” instead of raw exceptions.
- “Your iPod has not been changed” on completion and cancellation screens.

---

# 12. Settings

Include a simple settings page with:

- Folder organisation template.
- Duplicate behaviour.
- Verify copied files using SHA-256: on by default.
- Preserve source file timestamps: on by default.
- Include protected files in raw backup: on by default.
- Create an M3U8 playlist containing all exported tracks: on by default.
- Create playlists found on the iPod: on where supported.
- Remember last destination folder: on by default.
- Theme: System, Light, Dark.
- Anonymous diagnostics: off by default; do not implement network telemetry in the first version.

Store preferences under the user's local application data folder.

---

# 13. Reports and logs

Create a human-readable backup report in the destination root:

```text
NanoVault Backup Report - 2026-07-02.html
```

Also allow JSON export for technical use.

The report should include:

- Backup date and time.
- Application version.
- Non-sensitive device name.
- Destination.
- Total tracks found.
- Copied tracks.
- Skipped duplicates.
- Protected files.
- Failed files.
- Per-track result and final relative path.
- SHA-256 hash when verification is enabled.

Application diagnostic logs should go to:

```text
%LOCALAPPDATA%\NanoVault\Logs
```

Automatically retain a limited number of log files, such as 14 days.

Never log complete personal library metadata at information level unless needed for the backup report. Avoid logging serial numbers or account details.

---

# 14. Error handling

Handle these cases gracefully:

- No iPod connected.
- Multiple possible iPods.
- Device disappears during scan.
- Device disappears during copy.
- Destination drive runs out of space.
- Destination folder becomes unavailable.
- Source file is corrupted.
- Access denied.
- Path too long.
- Malformed iPod database.
- Unsupported file type.
- Protected AAC file.
- Antivirus temporarily locks a destination file.
- The app crashes or PC restarts during backup.

Before copying, check destination free space and require a sensible safety margin.

If disconnected, keep completed files, mark the operation as interrupted, and allow the user to reconnect and resume. Resume should rescan and skip files already verified at the destination.

Show a **Copy technical details** expander for errors, while keeping the main message understandable.

---

# 15. Architecture

Use a solution structure similar to:

```text
NanoVault/
├─ src/
│  ├─ NanoVault.App/                 # WPF UI and composition root
│  ├─ NanoVault.Core/                # Entities, interfaces, policies
│  ├─ NanoVault.Infrastructure/      # Windows volumes, filesystem, metadata
│  ├─ NanoVault.Ipod/                # iPod discovery and database parsing
│  └─ NanoVault.Installer/           # Installer definitions/scripts
├─ tests/
│  ├─ NanoVault.Core.Tests/
│  ├─ NanoVault.Infrastructure.Tests/
│  ├─ NanoVault.Ipod.Tests/
│  └─ NanoVault.IntegrationTests/
├─ test-data/
│  └─ SyntheticIpod/                 # Generated, copyright-free test fixture
├─ docs/
├─ build/
├─ README.md
├─ LICENSE
└─ NanoVault.sln
```

## Core services

Create clear interfaces such as:

```csharp
public interface IIpodDiscoveryService;
public interface IIpodScanner;
public interface IIpodDatabaseReader;
public interface IAudioMetadataReader;
public interface IBackupPlanner;
public interface ITrackCopyService;
public interface IFileVerificationService;
public interface IDuplicateResolver;
public interface IBackupReportWriter;
public interface ISettingsService;
```

## Important models

```csharp
IpodDevice
DiscoveredTrack
TrackMetadata
BackupPlan
BackupPlanItem
BackupProgress
BackupResult
TrackBackupResult
BackupWarning
```

Keep filesystem and Windows APIs outside view models. View models should coordinate services through commands and observable state.

---

# 16. Device detection

Implement both:

- Initial enumeration of mounted drives when the app opens.
- Automatic refresh when a volume arrives or is removed.

Use supported Windows mechanisms such as WMI volume-change notifications, device notifications, or periodic fallback enumeration. Dispose watchers correctly.

For every candidate volume:

1. Confirm it is ready.
2. Check the expected iPod folder structure safely.
3. Gather volume information without blocking the UI.
4. Score detection confidence.
5. Present ambiguous candidates rather than guessing destructively.

Do not require administrator rights merely to enumerate readable removable volumes.

---

# 17. Copy engine requirements

The copy engine must:

- Use asynchronous streaming.
- Avoid loading whole audio files into memory.
- Use a configurable buffer of reasonable size.
- Support pause, resume, and cancellation.
- Limit concurrent copies; default to one or two to avoid overwhelming an old flash device.
- Calculate progress from bytes copied.
- Retry transient destination errors a small number of times.
- Never endlessly retry a damaged source.
- Preserve the original extension.
- Preserve original timestamps where enabled.
- Verify file size and SHA-256 after copy where enabled.

Prefer reliability over maximum benchmark speed.

---

# 18. Playlist generation

Generate a UTF-8 `.m3u8` playlist in the backup root named:

```text
All iPod Music.m3u8
```

Use relative paths so the backup folder can be moved.

Where playlist metadata can be recovered reliably from the iPod database, generate additional playlists under:

```text
Playlists\<Playlist Name>.m3u8
```

Sanitise playlist names and skip missing tracks cleanly.

---

# 19. Tests

Create comprehensive automated tests.

## Unit tests

Test:

- iPod candidate scoring.
- Path normalisation.
- Metadata merging precedence.
- Invalid character sanitisation.
- Reserved Windows filenames.
- Long filename shortening.
- Organisation templates.
- Duplicate resolution.
- Hash comparison.
- M3U8 output.
- Backup result aggregation.
- Defensive database parsing.

## Integration tests

Build a synthetic iPod fixture with this shape:

```text
SyntheticIpod\
└─ iPod_Control\
   ├─ Music\
   │  ├─ F00\
   │  └─ F01\
   └─ iTunes\
```

Generate small copyright-free audio test files programmatically or include tiny generated tone files with appropriate licensing.

Test:

- Full scan.
- Metadata recovery.
- Organised copy.
- Duplicate skip.
- Resume after cancellation.
- Corrupt file handling.
- Missing metadata fallback.
- Device removal simulation through an injectable filesystem abstraction.

## UI tests

At minimum test view-model state transitions for:

- No device.
- Device found.
- Scan complete.
- Copy in progress.
- Cancelled.
- Completed with warnings.
- Fatal destination error.

---

# 20. Installer and release output

Produce a proper installer called similar to:

```text
NanoVault-Setup-1.0.0.exe
```

Installer requirements:

- Installs for the current user by default.
- Does not require admin rights unless the chosen install mode genuinely needs them.
- Start menu shortcut.
- Optional desktop shortcut.
- Clean uninstall entry in Windows Settings.
- Includes application icon and version information.
- Does not install browser extensions, services, advertisements, or unrelated software.
- Does not bundle music or Apple software.

Publish a self-contained, trimmed only if testing proves trimming does not break reflection or metadata libraries.

Create repeatable scripts:

```text
build\build.ps1
build\test.ps1
build\package.ps1
```

A clean machine should be able to run the packaging process after installing the documented development prerequisites.

---

# 21. README requirements

The repository README must include:

- What NanoVault does.
- A screenshot.
- Supported Windows versions.
- Supported iPod behaviour.
- Exact build instructions.
- Exact test instructions.
- Exact packaging instructions.
- Troubleshooting when the iPod does not appear as a drive.
- Privacy statement.
- Explanation that NanoVault does not alter the iPod.
- Explanation that protected files are not decrypted.
- Known limitations.

Do not claim support for a device unless it has been tested or the support is clearly marked as best effort.

---

# 22. Privacy and network behaviour

The first release must work entirely offline.

- No account creation.
- No advertisements.
- No analytics SDK.
- No music metadata uploaded to a server.
- No album artwork lookup over the internet.
- No automatic cloud backup.

The only files created should be the application installation, local settings/logs, and the user's selected backup output.

---

# 23. Acceptance criteria

The project is complete only when all of the following are true:

1. A Windows 10/11 user can install the app through a normal setup executable.
2. The app launches without a development environment.
3. A mounted iPod nano 4th generation with `iPod_Control\Music` is detected automatically.
4. The user can select any writable local destination folder.
5. The app discovers the music without requiring hidden-file settings.
6. Random iPod filenames are replaced with useful organised names when metadata is available.
7. The original encoded audio bytes are copied without transcoding.
8. The application never writes to the iPod.
9. Duplicate files are never silently overwritten.
10. Progress, pause, cancellation, and completion states work.
11. A disconnected iPod produces a recoverable interrupted state rather than corrupting completed files.
12. Failed tracks are reported while successful tracks remain backed up.
13. The backup report is generated.
14. The generated M3U8 playlist works with relative paths.
15. Automated tests pass.
16. The release build contains no placeholder controls, sample-device data, or unfinished screens.
17. The UI looks polished at 100%, 125%, 150%, and 200% Windows scaling.
18. The repository contains a repeatable build and packaging process.

---

# 24. Implementation order

Follow this sequence:

## Phase 1 — Foundation

- Create solution and projects.
- Configure MVVM, dependency injection, logging, settings, and tests.
- Create the main visual shell and navigation.

## Phase 2 — Device discovery

- Implement mounted-volume enumeration.
- Implement iPod candidate detection.
- Implement arrival/removal monitoring.
- Build device-status UI.

## Phase 3 — Track scanning

- Find media files.
- Read embedded metadata.
- Implement database-reader abstraction and fallback parsing.
- Build scan summary and track list.

## Phase 4 — Backup planning

- Build safe output paths.
- Add organisation templates.
- Add duplicate policy.
- Add free-space validation.

## Phase 5 — Copy engine

- Implement partial-file copying.
- Add byte progress, speed, pause, cancellation, retry, and resume.
- Add SHA-256 verification.

## Phase 6 — Reports and playlists

- Generate HTML and JSON reports.
- Generate M3U8 playlists.
- Add completion workflow.

## Phase 7 — Polish

- Accessibility.
- Dark mode.
- Empty states.
- Friendly errors.
- Icons and branding.
- Performance testing with at least 10,000 synthetic tracks.

## Phase 8 — Installer and release

- Build self-contained x64 release.
- Create installer.
- Test install, launch, backup, and uninstall on a clean Windows VM.
- Finish README and release notes.

---

# 25. Final quality instruction

Treat this as a real consumer utility entrusted with irreplaceable old music libraries. Prioritise safety, clarity, and recovery over cleverness.

Do not write to the source iPod under any circumstances. Do not hide errors, invent metadata, or claim successful verification when it has not occurred.

Before declaring the work complete, perform a final review against every acceptance criterion, run all tests, build the installer, and record any genuine limitations in the README.
