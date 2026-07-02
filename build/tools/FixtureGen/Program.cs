// Materialises the SyntheticIpod fixture into test-data/SyntheticIpod so a
// developer can point NanoVault at a fake iPod without a real device.
// All audio is generated programmatically (sine tones / silent frames) and
// carries no copyright. Run: dotnet run --project build/tools/FixtureGen -- <target-dir>

using NanoVault.TestSupport;

var target = args.Length > 0 ? args[0] : Path.Combine("test-data", "SyntheticIpod");

if (Directory.Exists(target))
{
    Directory.Delete(target, recursive: true);
}

// Not wrapped in `using`: disposing SyntheticIpod would delete the fixture.
var ipod = new SyntheticIpod(Path.GetFullPath(target));

var one = ipod.MusicFile("F00", "ABCD.mp3");
AudioFixtures.CreateMp3(one, frames: 40);
AudioFixtures.Tag(one, title: "First Tone", artist: "Synthetic Artist", album: "Fixture Album",
    track: 1, trackCount: 3, year: 2008, genre: "Test Tone");

var two = ipod.MusicFile("F00", "EFGH.mp3");
AudioFixtures.CreateMp3(two, frames: 40);
AudioFixtures.Tag(two, title: "Second Tone", artist: "Synthetic Artist", album: "Fixture Album",
    track: 2, trackCount: 3, year: 2008, genre: "Test Tone");

var three = ipod.MusicFile("F01", "IJKL.wav");
AudioFixtures.CreateWav(three);
// No embedded tags: this track's names come from the iPod database below.

var four = ipod.MusicFile("F01", "MNOP.wav");
AudioFixtures.CreateWav(four);
// No tags and no database entry: exercises safe fallback naming.

ipod.WriteDatabase(new ITunesDbBuilder()
    .AddTrack(new ITunesDbBuilder.TrackSpec(1, "First Tone", "Synthetic Artist", "Fixture Album",
        ":iPod_Control:Music:F00:ABCD.mp3", TrackNumber: 1, TrackCount: 3, Year: 2008))
    .AddTrack(new ITunesDbBuilder.TrackSpec(2, "Second Tone", "Synthetic Artist", "Fixture Album",
        ":iPod_Control:Music:F00:EFGH.mp3", TrackNumber: 2, TrackCount: 3, Year: 2008))
    .AddTrack(new ITunesDbBuilder.TrackSpec(3, "Database Tone", "Database Artist", "Fixture Album",
        ":iPod_Control:Music:F01:IJKL.wav", TrackNumber: 3, TrackCount: 3, Year: 2008))
    .AddPlaylist("All Fixtures", isMaster: true, 1, 2, 3)
    .AddPlaylist("Favourite Tones", isMaster: false, 2, 3));

Console.WriteLine($"Synthetic iPod written to {Path.GetFullPath(target)}");
