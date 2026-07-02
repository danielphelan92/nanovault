# SyntheticIpod fixture

A fake iPod for development and manual testing, with the exact on-disk shape
of a classic iPod nano:

```text
SyntheticIpod\
└─ iPod_Control\
   ├─ Music\
   │  ├─ F00\   ABCD.mp3, EFGH.mp3   (embedded ID3 tags)
   │  └─ F01\   IJKL.wav             (names come from the iTunesDB only)
   │            MNOP.wav             (no metadata anywhere → fallback naming)
   └─ iTunes\   iTunesDB             (real classic-format database with
                                      2 playlists, written by ITunesDbBuilder)
```

All audio is generated programmatically (sine tones and silent MPEG frames)
and carries no copyright.

Regenerate at any time:

```bash
dotnet run --project build/tools/FixtureGen -- test-data/SyntheticIpod
```

The integration tests do not read this folder — they generate their own
temporary fixtures via `NanoVault.TestSupport` — so feel free to modify it.
