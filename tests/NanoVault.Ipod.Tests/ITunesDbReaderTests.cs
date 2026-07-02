using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using NanoVault.Core.Policies;
using NanoVault.Infrastructure.FileSystem;
using NanoVault.Ipod.Database;
using NanoVault.TestSupport;
using Xunit;

namespace NanoVault.Ipod.Tests;

public class ITunesDbReaderTests : IDisposable
{
    private readonly SyntheticIpod _ipod = new();
    private readonly ITunesDbReader _reader = new(new PhysicalFileSystem(), NullLogger<ITunesDbReader>.Instance);

    public void Dispose() => _ipod.Dispose();

    private string DbPath => Path.Combine(_ipod.ITunesRoot, "iTunesDB");

    [Fact]
    public async Task Round_trip_reads_track_metadata_and_location()
    {
        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(
                Id: 1001,
                Title: "Näive Söng",           // non-ASCII survives UTF-16
                Artist: "The Artists",
                Album: "Grand Album",
                IpodPath: ":iPod_Control:Music:F00:ABCD.mp3",
                Genre: "Rock",
                AlbumArtist: "Album Artist",
                Composer: "A Composer",
                TrackNumber: 4,
                TrackCount: 12,
                DiscNumber: 1,
                DiscCount: 2,
                Year: 2004,
                DurationMs: 215_000,
                SizeBytes: 4_567_890,
                BitrateKbps: 192,
                SampleRateHz: 44100)));

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);

        var key = IpodPathNormalizer.ToComparableKey("iPod_Control/Music/F00/ABCD.mp3");
        Assert.True(tracks.ContainsKey(key));
        var track = tracks[key];

        Assert.Equal("Näive Söng", track.Title);
        Assert.Equal("The Artists", track.Artist);
        Assert.Equal("Grand Album", track.Album);
        Assert.Equal("Album Artist", track.AlbumArtist);
        Assert.Equal("A Composer", track.Composer);
        Assert.Equal("Rock", track.Genre);
        Assert.Equal(4, track.TrackNumber);
        Assert.Equal(12, track.TrackCount);
        Assert.Equal(1, track.DiscNumber);
        Assert.Equal(2, track.DiscCount);
        Assert.Equal(2004, track.Year);
        Assert.Equal(215_000, track.DurationMs);
        Assert.Equal(192, track.BitrateKbps);
        Assert.Equal(44100, track.SampleRateHz);
    }

    [Fact]
    public async Task Fixed_point_sample_rate_is_normalised()
    {
        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(
                1, "T", "A", "B", ":iPod_Control:Music:F00:X.mp3",
                SampleRateHz: 44100 << 16)));

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        Assert.Equal(44100, tracks.Values.Single().SampleRateHz);
    }

    [Fact]
    public async Task Playlists_resolve_track_ids_to_paths_and_master_is_flagged()
    {
        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(1, "One", "A", "Al", ":iPod_Control:Music:F00:AA.mp3"))
            .AddTrack(new ITunesDbBuilder.TrackSpec(2, "Two", "A", "Al", ":iPod_Control:Music:F01:BB.mp3"))
            .AddPlaylist("iPod", isMaster: true, 1, 2)
            .AddPlaylist("Road Trip", isMaster: false, 2, 1, 999 /* missing id skipped */));

        var playlists = await _reader.ReadPlaylistsAsync(_ipod.Root, CancellationToken.None);

        Assert.Equal(2, playlists.Count);
        var master = playlists.Single(p => p.IsMaster);
        Assert.Equal("iPod", master.Name);

        var roadTrip = playlists.Single(p => !p.IsMaster);
        Assert.Equal("Road Trip", roadTrip.Name);
        Assert.Equal(2, roadTrip.TrackRelativePaths.Count);
        Assert.Contains(roadTrip.TrackRelativePaths, p => p.EndsWith("BB.mp3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_database_returns_empty()
    {
        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task Garbage_file_is_rejected_without_crashing()
    {
        File.WriteAllBytes(DbPath, [1, 2, 3, 4, 5, 6, 7, 8]);
        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task Truncated_database_fails_gracefully()
    {
        var valid = new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(1, "T", "A", "B", ":iPod_Control:Music:F00:X.mp3"))
            .Build();
        File.WriteAllBytes(DbPath, valid.Take(valid.Length / 3).ToArray());

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        Assert.NotNull(tracks); // no throw; partial or empty is fine
    }

    [Fact]
    public async Task Corrupt_record_fails_individually_not_the_whole_scan()
    {
        var db = new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(1, "Good One", "A", "B", ":iPod_Control:Music:F00:GOOD.mp3"))
            .AddTrack(new ITunesDbBuilder.TrackSpec(2, "Good Two", "A", "B", ":iPod_Control:Music:F00:GOOD2.mp3"))
            .Build();

        // Corrupt the second mhit's totalLen so it claims to extend past the file.
        var second = IndexOfSecond(db, "mhit");
        Assert.True(second > 0);
        BitConverter.GetBytes(int.MaxValue).CopyTo(db, second + 8);
        File.WriteAllBytes(DbPath, db);

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        Assert.Single(tracks);
        Assert.Equal("Good One", tracks.Values.Single().Title);
    }

    [Fact]
    public async Task Insane_string_length_in_mhod_is_ignored()
    {
        var db = new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(1, "Title", "Artist", "Album", ":iPod_Control:Music:F00:X.mp3"))
            .Build();

        // Find the first mhod and set its string byte-length to a huge value.
        var mhod = IndexOf(db, "mhod", 0);
        Assert.True(mhod > 0);
        BitConverter.GetBytes(0x7FFFFFF0u).CopyTo(db, mhod + 0x18 + 4);
        File.WriteAllBytes(DbPath, db);

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        // The record survives; only the poisoned string is dropped.
        Assert.Single(tracks);
    }

    [Fact]
    public async Task Compressed_itunescdb_payload_is_read()
    {
        var raw = new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(1, "Zipped", "A", "B", ":iPod_Control:Music:F00:Z.mp3"))
            .Build();

        var headerLen = BitConverter.ToInt32(raw, 4);
        using var output = new MemoryStream();
        output.Write(raw, 0, headerLen);
        using (var zlib = new ZLibStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(raw, headerLen, raw.Length - headerLen);
        }

        File.WriteAllBytes(DbPath, output.ToArray());

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        Assert.Single(tracks);
        Assert.Equal("Zipped", tracks.Values.Single().Title);
    }

    [Fact]
    public async Task Bad_year_and_numbers_are_discarded()
    {
        _ipod.WriteDatabase(new ITunesDbBuilder()
            .AddTrack(new ITunesDbBuilder.TrackSpec(
                1, "T", "A", "B", ":iPod_Control:Music:F00:X.mp3",
                Year: 40000, TrackNumber: 5000)));

        var tracks = await _reader.ReadTracksAsync(_ipod.Root, CancellationToken.None);
        var track = tracks.Values.Single();
        Assert.Null(track.Year);
        Assert.Null(track.TrackNumber);
    }

    private static int IndexOf(byte[] data, string id, int from)
    {
        for (var i = from; i < data.Length - 4; i++)
        {
            if (data[i] == id[0] && data[i + 1] == id[1] && data[i + 2] == id[2] && data[i + 3] == id[3])
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfSecond(byte[] data, string id)
    {
        var first = IndexOf(data, id, 0);
        return first < 0 ? -1 : IndexOf(data, id, first + 4);
    }
}
