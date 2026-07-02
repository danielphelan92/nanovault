using System.Buffers.Binary;
using System.Text;

namespace NanoVault.TestSupport;

/// <summary>
/// Generates tiny, copyright-free audio files programmatically for tests:
/// PCM sine-wave WAVs and minimal silent MP3s, optionally tagged with
/// TagLibSharp.
/// </summary>
public static class AudioFixtures
{
    /// <summary>Writes a 0.2s 440Hz mono 16-bit 8kHz WAV file.</summary>
    public static void CreateWav(string path)
    {
        const int sampleRate = 8000;
        const int samples = sampleRate / 5;
        var dataSize = samples * 2;

        var buffer = new byte[44 + dataSize];
        var span = buffer.AsSpan();

        Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataSize);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(buffer, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(buffer, 12);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);            // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1);            // mono
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        Encoding.ASCII.GetBytes("data").CopyTo(buffer, 36);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataSize);

        for (var i = 0; i < samples; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 8000);
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + i * 2)..], value);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, buffer);
    }

    /// <summary>Writes a minimal valid MP3: several MPEG-1 Layer III frames of silence.</summary>
    public static void CreateMp3(string path, int frames = 8)
    {
        // 128 kbps, 44.1 kHz, no padding → 417-byte frames.
        const int frameLength = 417;
        var data = new byte[frames * frameLength];
        for (var i = 0; i < frames; i++)
        {
            var offset = i * frameLength;
            data[offset] = 0xFF;
            data[offset + 1] = 0xFB;
            data[offset + 2] = 0x90;
            data[offset + 3] = 0x00;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    /// <summary>Writes a file that is not valid audio at all.</summary>
    public static void CreateCorrupt(string path, int length = 4096)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)(i * 31 % 251);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    /// <summary>Adds embedded tags using TagLibSharp (fixture files only, never a device).</summary>
    public static void Tag(
        string path,
        string? title = null,
        string? artist = null,
        string? album = null,
        string? albumArtist = null,
        string? genre = null,
        uint track = 0,
        uint trackCount = 0,
        uint disc = 0,
        uint discCount = 0,
        uint year = 0)
    {
        using var file = TagLib.File.Create(path);
        if (title is not null)
        {
            file.Tag.Title = title;
        }

        if (artist is not null)
        {
            file.Tag.Performers = [artist];
        }

        if (album is not null)
        {
            file.Tag.Album = album;
        }

        if (albumArtist is not null)
        {
            file.Tag.AlbumArtists = [albumArtist];
        }

        if (genre is not null)
        {
            file.Tag.Genres = [genre];
        }

        if (track > 0)
        {
            file.Tag.Track = track;
        }

        if (trackCount > 0)
        {
            file.Tag.TrackCount = trackCount;
        }

        if (disc > 0)
        {
            file.Tag.Disc = disc;
        }

        if (discCount > 0)
        {
            file.Tag.DiscCount = discCount;
        }

        if (year > 0)
        {
            file.Tag.Year = year;
        }

        file.Save();
    }
}
