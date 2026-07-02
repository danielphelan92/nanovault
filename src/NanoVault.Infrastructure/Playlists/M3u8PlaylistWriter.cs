using System.Text;
using Microsoft.Extensions.Logging;
using NanoVault.Core.Abstractions;
using NanoVault.Core.Models;
using NanoVault.Core.Policies;

namespace NanoVault.Infrastructure.Playlists;

/// <summary>
/// Writes UTF-8 M3U8 playlists with relative paths so the whole backup folder
/// can be moved anywhere and the playlists keep working.
/// </summary>
public sealed class M3u8PlaylistWriter : IPlaylistWriter
{
    public const string MasterPlaylistName = "All iPod Music.m3u8";
    public const string PlaylistsFolderName = "Playlists";

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<M3u8PlaylistWriter> _logger;

    public M3u8PlaylistWriter(IFileSystem fileSystem, ILogger<M3u8PlaylistWriter> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<string?> WriteMasterPlaylistAsync(
        string destinationRoot,
        IReadOnlyList<TrackBackupResult> results,
        CancellationToken cancellationToken = default)
    {
        var entries = ExportedTracks(results).ToList();
        if (entries.Count == 0)
        {
            return null;
        }

        var path = Path.Combine(destinationRoot, MasterPlaylistName);
        await WritePlaylistAsync(path, entries.Select(e => (e.Track, e.FinalRelativePath!)), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Wrote {Playlist} with {Count} tracks", MasterPlaylistName, entries.Count);
        return MasterPlaylistName;
    }

    public async Task<IReadOnlyList<string>> WriteIpodPlaylistsAsync(
        string destinationRoot,
        IReadOnlyList<IpodPlaylist> playlists,
        IReadOnlyList<TrackBackupResult> results,
        CancellationToken cancellationToken = default)
    {
        // Map source-relative path → final destination-relative path.
        var finalPaths = new Dictionary<string, TrackBackupResult>(StringComparer.Ordinal);
        foreach (var result in ExportedTracks(results))
        {
            finalPaths[IpodPathNormalizer.ToComparableKey(result.Track.RelativeSourcePath)] = result;
        }

        var written = new List<string>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var playlist in playlists)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var members = playlist.TrackRelativePaths
                .Select(p => finalPaths.GetValueOrDefault(IpodPathNormalizer.ToComparableKey(p)))
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();

            if (members.Count == 0)
            {
                continue;
            }

            var stem = PathSanitizer.SanitizeComponent(playlist.Name, "Playlist", 80);
            if (!usedNames.Add(stem))
            {
                var i = 2;
                while (!usedNames.Add($"{stem} ({i})"))
                {
                    i++;
                }

                stem = $"{stem} ({i})";
            }

            var relativePath = Path.Combine(PlaylistsFolderName, stem + ".m3u8");
            var fullPath = Path.Combine(destinationRoot, relativePath);

            // Entries are relative to the playlist file's own folder.
            await WritePlaylistAsync(
                fullPath,
                members.Select(m => (m.Track, Path.Combine("..", m.FinalRelativePath!))),
                cancellationToken).ConfigureAwait(false);

            written.Add(relativePath);
        }

        if (written.Count > 0)
        {
            _logger.LogInformation("Wrote {Count} iPod playlists", written.Count);
        }

        return written;
    }

    private static IEnumerable<TrackBackupResult> ExportedTracks(IReadOnlyList<TrackBackupResult> results) =>
        results.Where(r =>
            r.FinalRelativePath is not null
            && r.Outcome is TrackOutcome.Copied or TrackOutcome.SkippedDuplicate);

    private async Task WritePlaylistAsync(
        string path,
        IEnumerable<(DiscoveredTrack Track, string RelativePath)> entries,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("#EXTM3U");

        foreach (var (track, relativePath) in entries)
        {
            var seconds = (int)Math.Round(track.Metadata.Duration?.TotalSeconds ?? -1);
            var label = track.DisplayName;
            builder.Append("#EXTINF:").Append(seconds).Append(',').AppendLine(label);
            builder.AppendLine(relativePath);
        }

        var stream = _fileSystem.CreateWrite(path);
        await using (stream.ConfigureAwait(false))
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }
}
