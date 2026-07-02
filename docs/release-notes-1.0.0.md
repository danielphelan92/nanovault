# NanoVault 1.0.0

First release.

## Highlights

- Automatic detection of classic disk-mode iPods (iPod nano 4G primary
  target) using folder structure, the iPod database, volume label, and
  removable-volume signals — no fixed drive letter or label assumptions.
- Read-only scan of `iPod_Control\Music` with layered metadata: embedded tags
  first, the iPod's own iTunesDB second, safe fallback naming last.
- Organised export (`Artist\Album\01 - Track Title.ext` plus two alternative
  templates, multi-disc handling, full Windows filename sanitisation).
- Duplicate safety: identical files skipped, name clashes renamed, replacing
  only ever by explicit choice — never a silent overwrite.
- Reliable copy engine: streamed partial files with atomic rename, SHA-256
  verification, timestamp preservation, pause/cancel, bounded retries,
  resume after cancellation or device disconnect.
- Outputs: `All iPod Music.m3u8` (relative paths), recovered iPod playlists,
  HTML + JSON backup report.
- Polished WPF UI with light/dark themes, keyboard accessibility, and
  friendly plain-language errors. Fully offline; no telemetry.

## Known limitations

See the *Known limitations* section of the README — most importantly, this
build has not yet been validated on real iPod hardware or a clean Windows VM,
and the installer is unsigned.
