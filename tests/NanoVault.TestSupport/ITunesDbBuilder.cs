using System.Buffers.Binary;
using System.Text;

namespace NanoVault.TestSupport;

/// <summary>
/// Builds valid classic-format iTunesDB bytes for tests. Layout follows the
/// same chunk structure the reader parses: mhbd → mhsd(1) → mhlt → mhit →
/// mhod, and mhsd(2) → mhlp → mhyp → mhod/mhip.
/// </summary>
public sealed class ITunesDbBuilder
{
    public sealed record TrackSpec(
        uint Id,
        string? Title,
        string? Artist,
        string? Album,
        string IpodPath,
        string? Genre = null,
        string? AlbumArtist = null,
        string? Composer = null,
        int TrackNumber = 0,
        int TrackCount = 0,
        int DiscNumber = 0,
        int DiscCount = 0,
        int Year = 0,
        int DurationMs = 0,
        int SizeBytes = 0,
        int BitrateKbps = 0,
        int SampleRateHz = 0);

    public sealed record PlaylistSpec(string Name, bool IsMaster, uint[] TrackIds);

    private readonly List<TrackSpec> _tracks = new();
    private readonly List<PlaylistSpec> _playlists = new();

    public ITunesDbBuilder AddTrack(TrackSpec track)
    {
        _tracks.Add(track);
        return this;
    }

    public ITunesDbBuilder AddPlaylist(string name, bool isMaster, params uint[] trackIds)
    {
        _playlists.Add(new PlaylistSpec(name, isMaster, trackIds));
        return this;
    }

    public byte[] Build()
    {
        var trackSection = BuildTrackSection();
        var playlistSection = BuildPlaylistSection();

        const int mhbdHeaderLen = 0x68;
        var total = mhbdHeaderLen + trackSection.Length + playlistSection.Length;

        var db = new byte[total];
        WriteChunkHeader(db, 0, "mhbd", mhbdHeaderLen, total);
        // child count at 0x14
        BinaryPrimitives.WriteUInt32LittleEndian(db.AsSpan(0x14), 2);

        trackSection.CopyTo(db, mhbdHeaderLen);
        playlistSection.CopyTo(db, mhbdHeaderLen + trackSection.Length);
        return db;
    }

    private byte[] BuildTrackSection()
    {
        var items = _tracks.Select(BuildMhit).ToList();
        var itemsLength = items.Sum(i => i.Length);

        const int mhltHeaderLen = 0x5C;
        var mhlt = new byte[mhltHeaderLen];
        WriteChunkId(mhlt, 0, "mhlt");
        BinaryPrimitives.WriteUInt32LittleEndian(mhlt.AsSpan(4), mhltHeaderLen);
        BinaryPrimitives.WriteUInt32LittleEndian(mhlt.AsSpan(8), (uint)items.Count);

        const int mhsdHeaderLen = 0x60;
        var totalLen = mhsdHeaderLen + mhlt.Length + itemsLength;
        var section = new byte[totalLen];
        WriteChunkHeader(section, 0, "mhsd", mhsdHeaderLen, totalLen);
        BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(0x0C), 1); // type 1: tracks

        var offset = mhsdHeaderLen;
        mhlt.CopyTo(section, offset);
        offset += mhlt.Length;
        foreach (var item in items)
        {
            item.CopyTo(section, offset);
            offset += item.Length;
        }

        return section;
    }

    private static byte[] BuildMhit(TrackSpec track)
    {
        var mhods = new List<byte[]>();
        void AddString(uint type, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                mhods.Add(BuildStringMhod(type, value));
            }
        }

        AddString(1, track.Title);
        AddString(2, track.IpodPath);
        AddString(3, track.Album);
        AddString(4, track.Artist);
        AddString(5, track.Genre);
        AddString(12, track.Composer);
        AddString(22, track.AlbumArtist);

        const int headerLen = 0x184;
        var totalLen = headerLen + mhods.Sum(m => m.Length);
        var mhit = new byte[totalLen];
        WriteChunkHeader(mhit, 0, "mhit", headerLen, totalLen);

        void U32(int offset, int value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(mhit.AsSpan(offset), unchecked((uint)value));

        U32(0x0C, mhods.Count);
        U32(0x10, (int)track.Id);
        U32(0x24, track.SizeBytes);
        U32(0x28, track.DurationMs);
        U32(0x2C, track.TrackNumber);
        U32(0x30, track.TrackCount);
        U32(0x34, track.Year);
        U32(0x38, track.BitrateKbps);
        U32(0x3C, track.SampleRateHz);
        U32(0x60, track.DiscNumber);
        U32(0x64, track.DiscCount);

        var offset = headerLen;
        foreach (var mhod in mhods)
        {
            mhod.CopyTo(mhit, offset);
            offset += mhod.Length;
        }

        return mhit;
    }

    private byte[] BuildPlaylistSection()
    {
        var lists = _playlists.Select(BuildMhyp).ToList();
        var listsLength = lists.Sum(l => l.Length);

        const int mhlpHeaderLen = 0x5C;
        var mhlp = new byte[mhlpHeaderLen];
        WriteChunkId(mhlp, 0, "mhlp");
        BinaryPrimitives.WriteUInt32LittleEndian(mhlp.AsSpan(4), mhlpHeaderLen);
        BinaryPrimitives.WriteUInt32LittleEndian(mhlp.AsSpan(8), (uint)lists.Count);

        const int mhsdHeaderLen = 0x60;
        var totalLen = mhsdHeaderLen + mhlp.Length + listsLength;
        var section = new byte[totalLen];
        WriteChunkHeader(section, 0, "mhsd", mhsdHeaderLen, totalLen);
        BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(0x0C), 2); // type 2: playlists

        var offset = mhsdHeaderLen;
        mhlp.CopyTo(section, offset);
        offset += mhlp.Length;
        foreach (var list in lists)
        {
            list.CopyTo(section, offset);
            offset += list.Length;
        }

        return section;
    }

    private static byte[] BuildMhyp(PlaylistSpec playlist)
    {
        var children = new List<byte[]> { BuildStringMhod(1, playlist.Name) };
        children.AddRange(playlist.TrackIds.Select(BuildMhip));

        const int headerLen = 0x6C;
        var totalLen = headerLen + children.Sum(c => c.Length);
        var mhyp = new byte[totalLen];
        WriteChunkHeader(mhyp, 0, "mhyp", headerLen, totalLen);
        BinaryPrimitives.WriteUInt32LittleEndian(mhyp.AsSpan(0x0C), 1); // mhod count
        BinaryPrimitives.WriteUInt32LittleEndian(mhyp.AsSpan(0x10), (uint)playlist.TrackIds.Length);
        mhyp[0x14] = playlist.IsMaster ? (byte)1 : (byte)0;

        var offset = headerLen;
        foreach (var child in children)
        {
            child.CopyTo(mhyp, offset);
            offset += child.Length;
        }

        return mhyp;
    }

    private static byte[] BuildMhip(uint trackId)
    {
        const int headerLen = 0x4C;
        var mhip = new byte[headerLen];
        WriteChunkHeader(mhip, 0, "mhip", headerLen, headerLen);
        BinaryPrimitives.WriteUInt32LittleEndian(mhip.AsSpan(0x18), trackId);
        return mhip;
    }

    private static byte[] BuildStringMhod(uint type, string value)
    {
        var text = Encoding.Unicode.GetBytes(value);
        const int headerLen = 0x18;
        var totalLen = headerLen + 16 + text.Length;
        var mhod = new byte[totalLen];
        WriteChunkHeader(mhod, 0, "mhod", headerLen, totalLen);
        BinaryPrimitives.WriteUInt32LittleEndian(mhod.AsSpan(0x0C), type);
        BinaryPrimitives.WriteUInt32LittleEndian(mhod.AsSpan(headerLen), 1);                  // position
        BinaryPrimitives.WriteUInt32LittleEndian(mhod.AsSpan(headerLen + 4), (uint)text.Length);
        text.CopyTo(mhod, headerLen + 16);
        return mhod;
    }

    private static void WriteChunkHeader(byte[] buffer, int offset, string id, int headerLen, int totalLen)
    {
        WriteChunkId(buffer, offset, id);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 4), (uint)headerLen);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 8), (uint)totalLen);
    }

    private static void WriteChunkId(byte[] buffer, int offset, string id)
    {
        for (var i = 0; i < 4; i++)
        {
            buffer[offset + i] = (byte)id[i];
        }
    }
}
